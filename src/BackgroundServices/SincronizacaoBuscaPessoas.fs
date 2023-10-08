module Rinha.BackgroundService.Sync

open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging.Abstractions
open NATS.Client.Core

open Rinha
open Rinha.Handlers

type SincronizacaoBuscaPessoas(pessoasMap: IBuscaMap, pessoasById: IPessoasById, apelidoPessoas: IApelidoPessoas) =
    inherit BackgroundService()

    let _pessoasMap: IBuscaMap = pessoasMap
    let _logger: ILogger = NullLogger.Instance
    let natsOwnChannel = Environment.NATS_OWN_CHANNEL
    let _pessoasById: IPessoasById = pessoasById
    let _apelidoPessoas: IApelidoPessoas = apelidoPessoas

    // TODO improve implementation (it's different from the original source)
    // https://github.com/andr3marra/rinha-de-backend-2023-q3-csharp/blob/main/src/SincronizacaoBuscaPessoas.cs
    override this.ExecuteAsync(stoppingToken: CancellationToken) =
        task {
            let natsOptions =
                NatsOpts(
                    Rinha.Environment.NATS_URL, // Url
                    NatsOpts.Default.Name,
                    NatsOpts.Default.Echo,
                    NatsOpts.Default.Verbose,
                    NatsOpts.Default.Headers,
                    NatsOpts.Default.AuthOpts,
                    NatsOpts.Default.TlsOpts,
                    NatsOpts.Default.Serializer,
                    NullLoggerFactory.Instance, // LoggerFactory
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
                    50000, // ObjectPoolSize
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

            use! sub =
                natsConnection.SubscribeAsync<Dto.DatabasePessoaDto>(natsOwnChannel, "", natSubOpts, stoppingToken)

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
        }
