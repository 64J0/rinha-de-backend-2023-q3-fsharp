open System
open System.Threading.Channels
open System.Text.Json.Serialization
open System.Collections.Concurrent
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Console
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection

open Giraffe
open NATS.Client.Core
open NATS.Client.Hosting

open Rinha

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> ServerErrors.INTERNAL_ERROR $"Error: {ex.Message}"

let webApp =
    choose
        [ GET >=> route "/ping" >=> text "pong"
          POST >=> route "/pessoas" >=> Handlers.createPessoaHandler ()
          GET >=> routef "/pessoas/%s" Handlers.searchPessoaByIdHandler
          GET >=> route "/pessoas" >=> Handlers.searchPessoasByTHandler ()
          GET >=> route "/contagem-pessoas" >=> Handlers.countPessoasHandler ()
          setStatusCode 404 >=> text "Not found" ]

let configureApp (app: IApplicationBuilder) =
    app.UseGiraffeErrorHandler(errorHandler).UseGiraffe webApp

let configureServices (services: IServiceCollection) =
    let jsonOptions = JsonFSharpOptions.FSharpLuLike().ToJsonSerializerOptions()

    services
        .AddGiraffe()
        .AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(jsonOptions))
    |> ignore

    // TODO improve
    // https://github.com/andr3marra/rinha-de-backend-2023-q3-csharp/blob/main/src/Program.cs#L11
    services.AddNats(
        1,
        (fun (_options: NatsOpts) ->
            let natsOptions = NatsOpts.Default

            NatsOpts(
                Environment.GetEnvironmentVariable("NATS_URL"),
                natsOptions.Name,
                natsOptions.Echo,
                natsOptions.Verbose,
                natsOptions.Headers,
                natsOptions.AuthOpts,
                natsOptions.TlsOpts,
                natsOptions.Serializer,
                natsOptions.LoggerFactory,
                natsOptions.WriterBufferSize,
                natsOptions.ReaderBufferSize,
                natsOptions.UseThreadPoolCallback,
                natsOptions.InboxPrefix,
                natsOptions.NoRandomize,
                natsOptions.PingInterval,
                natsOptions.MaxPingOut,
                natsOptions.ReconnectWait,
                natsOptions.ReconnectJitter,
                natsOptions.ConnectTimeout,
                natsOptions.ObjectPoolSize,
                natsOptions.RequestTimeout,
                natsOptions.CommandTimeout,
                natsOptions.SubscriptionCleanUpInterval,
                natsOptions.WriterCommandBufferLimit,
                natsOptions.HeaderEncoding,
                natsOptions.WaitUntilSent
            ))
    )
    |> ignore

    let getBuscaMap: Handlers.IBuscaMap =
        fun () -> new ConcurrentDictionary<string, Dto.OutputPessoaDto>()

    let getPessoasById: Handlers.IPessoasById =
        fun () -> new ConcurrentDictionary<Guid, Dto.OutputPessoaDto>()

    let getApelidoPessoas: Handlers.IApelidoPessoas =
        fun () -> new ConcurrentDictionary<string, byte>()

    let getChannelPessoa: Handlers.IChannelPessoa =
        fun () ->
            let options = new UnboundedChannelOptions()
            options.SingleReader <- true
            Channel.CreateUnbounded<Dto.OutputPessoaDto>(options)

    // https://www.compositional-it.com/news-blog/dependency-injection-with-asp-net-and-f/
    // https://giraffe.wiki/docs#dependency-management
    // https://dev.to/jhewlett/dependency-injection-in-f-web-apis-4h2o
    services
        .AddSingleton<Handlers.IBuscaMap>(getBuscaMap)
        .AddSingleton<Handlers.IPessoasById>(getPessoasById)
        .AddSingleton<Handlers.IApelidoPessoas>(getApelidoPessoas)
        .AddSingleton<Handlers.IChannelPessoa>(getChannelPessoa)
    |> ignore

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
