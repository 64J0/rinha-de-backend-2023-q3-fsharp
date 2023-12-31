module Rinha.BackgroundService.Insert

open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open Microsoft.Extensions.Hosting
open Npgsql

open Rinha
open Rinha.Handlers

type InsercaoRegistrosPessoas(channel: IChannelPessoa, pessoasMap: IBuscaMap) =
    inherit BackgroundService()

    let _channel: IChannelPessoa = channel
    let _pessoasMap: IBuscaMap = pessoasMap
    let _conn: NpgsqlConnection = Rinha.Database.getDbConnection ()

    override this.ExecuteAsync(stoppingToken: CancellationToken) =
        task {
            let mutable connected = false

            while (not connected) do
                try
                    do! _conn.OpenAsync()
                    connected <- true
                    printfn "Connected to postgres!"
                with exn ->
                    printfn $"Exception: {exn}"
                    printfn "Retrying connection to postgres..."
                    do! Task.Delay 1_000

            let pessoas = new List<Dto.DatabasePessoaDto>()

            while (not stoppingToken.IsCancellationRequested) do
                let channelPessoas = _channel.Reader.ReadAllAsync().GetAsyncEnumerator()

                let mutable nxt = true

                while nxt do
                    let! nextPessoa = channelPessoas.MoveNextAsync()
                    nxt <- nextPessoa

                    if nxt then
                        pessoas.Add(channelPessoas.Current)

                        // TODO use if, although I'm not confident that this is the most reliable option
                        // for example, if the server crashes with 99 pessoas on the list, we'll lose them
                        // if (pessoas.Count > 100) then
                        try
                            let batch = _conn.CreateBatch()

                            for p in pessoas do
                                let batchCmd =
                                    new NpgsqlBatchCommand(
                                        """
                                    insert into pessoas
                                    (id, apelido, nome, nascimento, stack)
                                    values ($1, $2, $3, $4, $5);
                                """
                                    )

                                batchCmd.Parameters.AddWithValue(p.id) |> ignore
                                batchCmd.Parameters.AddWithValue(p.apelido) |> ignore
                                batchCmd.Parameters.AddWithValue(p.nome) |> ignore
                                batchCmd.Parameters.AddWithValue(p.nascimento) |> ignore
                                batchCmd.Parameters.AddWithValue(p.stack) |> ignore
                                batch.BatchCommands.Add(batchCmd)

                            let! databaseResult = batch.ExecuteNonQueryAsync()
                            printfn $"Added new Pessoa to database: {databaseResult}"
                            pessoas.Clear()
                        with exn ->
                            printfn $"Error when storing batch of Pessoas on DB. {exn}"
                    // else
                    //     ()
                    else
                        ()

            do! _conn.CloseAsync()
            do! _conn.DisposeAsync()
        }
