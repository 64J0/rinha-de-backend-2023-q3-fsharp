module Rinha.Handlers

open System
open System.Data
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging

open FsToolkit.ErrorHandling
open Giraffe

open Rinha

let createPessoaHandler () =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let execute
            (logger: ILogger)
            (conn: IDbConnection)
            (serializer: Json.ISerializer)
            (inputPessoa: Dto.InputPessoaDto)
            =
            asyncResult {
                let! domainPessoa = Dto.InputPessoaDto.toDomain inputPessoa

                let databasePessoa =
                    Dto.DatabasePessoaDto.fromDomain (serializer.SerializeToString) (domainPessoa)

                return! Repository.insertPessoa logger conn databasePessoa
            }

        task {
            let conn: IDbConnection = Database.getDbConnection ()
            let logger: ILogger = ctx.GetLogger()
            let serializer: Json.ISerializer = ctx.GetJsonSerializer()

            use _ = logger.BeginScope("CreatePessoaHandler")

            let! input = ctx.ReadBodyBufferedFromRequestAsync()
            let inputPessoa = serializer.Deserialize<Dto.InputPessoaDto> input

            let! result = execute logger conn serializer inputPessoa

            match result with
            | Ok dbVal ->
                ctx.SetStatusCode 201
                return! text $"Pessoa inserted, return code {dbVal}" next ctx
            | Error err ->
                ctx.SetStatusCode 422
                return! text $"Error when inserting pessoa: {err}" next ctx
        }

let searchPessoasByTHandler () =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let term = ctx.TryGetQueryStringValue "t"

            match term with
            | Some t ->
                let conn: IDbConnection = Database.getDbConnection ()
                let logger: ILogger = ctx.GetLogger()
                let serializer: Json.ISerializer = ctx.GetJsonSerializer()

                use _ = logger.BeginScope("SearchPessoasByTHandler")

                let! databasePessoas = Repository.searchPessoasByT logger conn t

                match databasePessoas with
                | Ok pessoas ->
                    let outputPessoas =
                        pessoas |> Seq.map (Dto.OutputPessoaDto.fromDatabaseDto serializer.Deserialize)

                    let serializedPessoas = serializer.SerializeToString outputPessoas

                    ctx.SetStatusCode 200
                    return! text serializedPessoas next ctx
                | Error err ->
                    ctx.SetStatusCode 500
                    return! text err next ctx
            | None ->
                ctx.SetStatusCode 400
                return! text "Please inform 't'" next ctx
        }

let searchPessoaByIdHandler (input: string) =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let id = input
            // TODO add validation
            let parsedId: Guid = Guid.Parse id
            let conn: IDbConnection = Database.getDbConnection ()
            let logger: ILogger = ctx.GetLogger()
            let serializer: Json.ISerializer = ctx.GetJsonSerializer()

            use _ = logger.BeginScope("SearchPessoaByIdHandler")

            let! databasePessoas = Repository.searchPessoaById logger conn parsedId

            match databasePessoas with
            | Ok pessoas ->
                let outputPessoas =
                    pessoas |> Seq.map (Dto.OutputPessoaDto.fromDatabaseDto serializer.Deserialize)

                let firstPessoa = Seq.tryHead outputPessoas

                match firstPessoa with
                | Some p ->
                    let serializedPessoa = serializer.SerializeToString p

                    ctx.SetStatusCode 200
                    return! text serializedPessoa next ctx
                | None ->
                    ctx.SetStatusCode 404
                    return! text "Not Found" next ctx
            | Error err ->
                ctx.SetStatusCode 500
                return! text err next ctx
        }

let countPessoasHandler () =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let conn: IDbConnection = Database.getDbConnection ()
            let logger: ILogger = ctx.GetLogger()

            use _ = logger.BeginScope("CountPessoasHandler")

            let! databaseResult = Repository.countPessoas logger conn

            match databaseResult with
            | Ok result ->
                let value = Seq.head result

                ctx.SetStatusCode 200
                return! text (sprintf "%i" value.Value) next ctx
            | Error err ->
                ctx.SetStatusCode 500
                return! text err next ctx
        }
