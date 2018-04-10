module WebsocketApp.App

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


let mainUrl = "http://0.0.0.0:5000"
let jsonPostsUrl = "http://0.0.0.0:3000/posts"

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open GiraffeViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "WebsocketApp" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "WebsocketApp" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

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

let demoHandler (name : string) =
    let txt = match name with
                | "test1" -> sprintf "Hello %s, from Giraffe!" name
                | "test2" -> test2Fun
                | "test3" -> test3Fun
                | _ -> name |> sampleFun

    Successful.ok (text txt)




 
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