module MyGiraffeApp.App

open System
open System.IO
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Hopac
open HttpFs.Client
open Microsoft.AspNetCore.Http
open Newtonsoft.Json
open Newtonsoft.Json.Serialization

let mainUrl = "http://0.0.0.0:5000"
let jsonPostsUrl = "http://0.0.0.0:3000/posts"

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }


[<CLIMutable>]
type Person = {
        ID      : int
        Name :  string
 }

[<CLIMutable>]
type Car =
    {
        Name   : string
        Make   : string
        Wheels : int
        Built  : DateTime option
    }

[<CLIMutable>]
type ListOfCar = 
    {
        Cars : Car list
        Version : string
        Length: int

    }

[<CLIMutable>]
type CarRequest = 
    {
        List: ListOfCar
    }



// ---------------------------------
// Views
// ---------------------------------

module Views =
    open GiraffeViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "MyGiraffeApp" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "MyGiraffeApp" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

let jsonSerializerSetting = JsonSerializerSettings(
                ContractResolver = CamelCasePropertyNamesContractResolver())

let indexHandler (name : string) =
    let greetings = sprintf "Hello %s, from Giraffe!" name
    let model     = { Text = greetings }
    let view      = Views.index model
    htmlView view

let sampleFun (name : string) =
    sprintf "Hello %s, from Giraffe!" name

let test2Fun =
    let daysList =
        [ for month in 1 .. 12 do
              for day in 1 .. System.DateTime.DaysInMonth(2012, month) do
                  yield System.DateTime(2012, month, day) ]
    daysList.ToString()

let printString (txt : string) =
    sprintf "%s" txt

let applyFn fn txt =
    fn txt

let test3Fun =
    applyFn printString "ok"


let jsonTestFun (txt : string)=
    let xs = [{ID = 1; Name = "First"} ; { ID = 2; Name = "Second"}]

    let json = JsonConvert.SerializeObject(xs, Formatting.Indented, jsonSerializerSetting)
    json |> printfn "%s"
     
    let xs1 = JsonConvert.DeserializeObject<Person list>(json)
    xs1 |> List.iter(fun x -> printfn "%i  %s" x.ID x.Name) 
     
    txt

let sortByFun (txt: string) =
    let persons2 = [{Name="Joe"; ID=120}; {Name="foo"; ID=31}; {Name="bar"; ID=51}]
    let sorted = List.sortBy (fun p -> p.ID) persons2
    for p in sorted do printfn "%A" p

    txt

let demoHandler (name : string) =
    let txt = match name with
                | "test1" -> sprintf "Hello %s, from Giraffe!" name
                | "test2" -> test2Fun
                | "test3" -> test3Fun
                | "jsontest" -> jsonTestFun name
                | "sortby" -> sortByFun name
                | _ -> name |> sampleFun

    Successful.ok (text txt)




 




let submitCar : HttpHandler = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            // Binds a JSON payload to a Car object
            let! car = ctx.BindJsonAsync<CarRequest>()
            // Sends the object back to the client
            return! Successful.OK car next ctx}

let remoteRequestHandler : HttpHandler = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let targetUrl = 
                match ctx.TryGetQueryStringValue "url" with
                | None -> "http://www.google.com"
                | Some q -> "http://" + q
            
            //printf "%s" someValue
            let body = 
                targetUrl
                |> Request.createUrl Get
                |> Request.responseAsString
                |> run
            
            return! ctx.WriteHtmlStringAsync body
        }

let remotePostRequest : HttpHandler = 
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let! bodyJson = ctx.ReadBodyFromRequestAsync()
            //printf "%s" bodyJson
           
            
            let body = 
                jsonPostsUrl
                |> Request.createUrl Post
                |> Request.body(BodyString bodyJson)
                |> Request.setHeader(ContentType(ContentType.create("application", "json")))
                |> Request.responseCharacterEncoding Encoding.UTF8
                |> Request.responseAsString
                |> run
            return! ctx.WriteTextAsync body
        }

// unit -> string
let time() = System.DateTime.Now.ToString()

let webApp = 
    choose [GET >=> choose [route "/" >=> indexHandler "world";
                            routef "/hello/%s" indexHandler;
                            route "/normal" >=> text(time()); // Only calculated once!
                            route "/warbler" >=> warbler(fun _ -> text(time()));
                            route "/get" >=> remoteRequestHandler
                            routef "/demo/%s" demoHandler;
                            ];
            POST >=> choose [route "/car" >=> submitCar;
                             route "/posts" >=> remotePostRequest];
            setStatusCode 404 >=> text "Not Found"]


// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://0.0.0.0:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IHostingEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(contentRoot)
        .UseIISIntegration()
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .UseUrls(mainUrl)
        .Build()
        .Run()
    0   