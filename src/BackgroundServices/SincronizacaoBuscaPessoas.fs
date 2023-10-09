module Rinha.BackgroundService.Sync

open System.Threading
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging.Abstractions
open NATS.Client.Core

open Rinha
open Rinha.Handlers

type SincronizacaoBuscaPessoas(pessoasMap: IBuscaMap, pessoasById: IPessoasById, apelidoPessoas: IApelidoPessoas) =
    inherit BackgroundService()

    let _pessoasMap: IBuscaMap = pessoasMap
    let _natsOwnChannel = Environment.NATS_OWN_CHANNEL
    let _pessoasById: IPessoasById = pessoasById
    let _apelidoPessoas: IApelidoPessoas = apelidoPessoas

    // TODO improve implementation (it's different from the original source)
    // https://github.com/andr3marra/rinha-de-backend-2023-q3-csharp/blob/main/src/SincronizacaoBuscaPessoas.cs
    override this.ExecuteAsync(stoppingToken: CancellationToken) =
        task {
            let natsUrl = Rinha.Environment.NATS_URL
            let natsLoggerFactory = NullLoggerFactory.Instance
            let natsObjectPoolSize = 50_000

            let natsOptions =
                NatsOpts(
                    natsUrl, // Url
                    NatsOpts.Default.Name,
                    NatsOpts.Default.Echo,
                    NatsOpts.Default.Verbose,
                    NatsOpts.Default.Headers,
                    NatsOpts.Default.AuthOpts,
                    NatsOpts.Default.TlsOpts,
                    NatsOpts.Default.Serializer,
                    natsLoggerFactory, // LoggerFactory
                    NatsOpts.Default.WriterBufferSize,
                    NatsOpts.Default.ReaderBufferSize,
                    NatsOpts.Default.UseThreadPoolCallback,
                    NatsOpts.Default.InboxPrefix,
                    NatsOpts.Default.NoRandomize,
                    NatsOpts.Default.PingInterval,
                    NatsOpts.Default.MaxPingOut,
                    NatsOpts.Default.ReconnectWait,
                    NatsOpts.Default.ReconnectJitter,
                    NatsOpts.Default.ConnectTimeout,
                    natsObjectPoolSize, // ObjectPoolSize
                    NatsOpts.Default.RequestTimeout,
                    NatsOpts.Default.CommandTimeout,
                    NatsOpts.Default.SubscriptionCleanUpInterval,
                    NatsOpts.Default.WriterCommandBufferLimit,
                    NatsOpts.Default.HeaderEncoding,
                    NatsOpts.Default.WaitUntilSent
                )

            let natsConnection = new NatsConnection(natsOptions)
            do! natsConnection.ConnectAsync()

            let natSubOpts = NatsSubOpts()

            try
                use! sub =
                    natsConnection.SubscribeAsync<Dto.DatabasePessoaDto>(_natsOwnChannel, "", natSubOpts, stoppingToken)

                let channelMsg = sub.Msgs.ReadAllAsync(stoppingToken).GetAsyncEnumerator()

                let mutable nxt = true

                while nxt do
                    let! nextMsg = channelMsg.MoveNextAsync()
                    nxt <- nextMsg

                    if nxt then
                        let msg = channelMsg.Current

                        let pessoa = msg.Data
                        let buscaStackValue = pessoa.stack
                        let buscaValue = $"{pessoa.apelido}{pessoa.nome}{buscaStackValue}"
                        _pessoasMap.TryAdd(buscaValue, pessoa) |> ignore
                        _pessoasById.TryAdd(pessoa.id, pessoa) |> ignore
                        _apelidoPessoas.TryAdd(pessoa.apelido, byte 0) |> ignore
                    else
                        ()
            with
            | :? System.NullReferenceException as ex -> printfn $"System.NullReferenceException\n{ex.Message}"
            | ex -> printfn $"{ex.Message}"
        }
