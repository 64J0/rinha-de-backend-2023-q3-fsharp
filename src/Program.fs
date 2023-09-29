open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Console
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection

open Giraffe
open System.Text.Json.Serialization
open NATS.Client.Core
open NATS.Client.Hosting

open Rinha.Handlers

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> ServerErrors.INTERNAL_ERROR $"Error: {ex.Message}"

let webApp =
    choose
        [ GET >=> route "/ping" >=> text "pong"
          POST >=> route "/pessoas" >=> createPessoaHandler ()
          GET >=> routef "/pessoas/%s" searchPessoaByIdHandler
          GET >=> route "/pessoas" >=> searchPessoasByTHandler ()
          GET >=> route "/contagem-pessoas" >=> countPessoasHandler ()
          setStatusCode 404 >=> text "Not found" ]

let configureApp (app: IApplicationBuilder) =
    app.UseGiraffeErrorHandler(errorHandler).UseGiraffe webApp

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore

    let jsonOptions = JsonFSharpOptions.FSharpLuLike().ToJsonSerializerOptions()

    services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(jsonOptions))
    |> ignore

// services.AddNats(1, configureOptions: options => NatsOptions.Default with { Url = Environment.GetEnvironmentVariable("NATS_URL") });

let configureLogger (logger: ILoggingBuilder) : unit =
    let consoleAction =
        System.Action<SimpleConsoleFormatterOptions>(fun options -> options.IncludeScopes <- true)

    logger.AddSimpleConsole(consoleAction).SetMinimumLevel LogLevel.Debug |> ignore

let configureWebHostDefaults (webHostBuilder: IWebHostBuilder) : unit =
    webHostBuilder
        .Configure(configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogger)
    |> ignore



[<EntryPoint>]
let main (_args: string[]) : int =
    Host
        .CreateDefaultBuilder()
        .ConfigureWebHostDefaults(configureWebHostDefaults)
        .Build()
        .Run()

    0
