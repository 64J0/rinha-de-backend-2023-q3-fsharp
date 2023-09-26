module Rinha.Handlers

open System
open System.Data
open System.Collections
open Microsoft.AspNetCore.Http
open Giraffe

open Rinha

let createPessoaHandler () =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let conn: IDbConnection = Database.getDbConnection ()
            let serializer = ctx.GetJsonSerializer()

            let! input = ctx.ReadBodyBufferedFromRequestAsync()
            let inputPessoa = serializer.Deserialize<Dto.InputPessoaDto> input

            let domainPessoa = Dto.InputPessoaDto.toDomain inputPessoa

            match domainPessoa with
            | Ok pessoa ->
                let databasePessoa = Dto.DatabasePessoaDto.fromDomain pessoa
                let! result = Repository.insertPessoa conn databasePessoa

                ctx.SetStatusCode 201
                return! text $"Pessoa inserted, return code {result}" next ctx
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
                let serializer = ctx.GetJsonSerializer()

                let! (databasePessoas: Generic.IEnumerable<Dto.DatabasePessoaDto>) = Repository.searchPessoasByT conn t
                let outputPessoas = databasePessoas |> Seq.map Dto.OutputPessoaDto.fromDatabaseDto

                let serializedPessoas = serializer.SerializeToString outputPessoas

                ctx.SetStatusCode 200
                return! text serializedPessoas next ctx
            | None ->
                ctx.SetStatusCode 400
                return! text "Please inform 't'" next ctx
        }

let searchPessoaByIdHandler (input: string) =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let id = input
            // TODO add validation
            let parsedId = Guid.Parse id
            let conn: IDbConnection = Database.getDbConnection ()
            let serializer = ctx.GetJsonSerializer()

            let! (databasePessoas: Generic.IEnumerable<Dto.DatabasePessoaDto>) =
                Repository.searchPessoaById conn parsedId

            let outputPessoas = databasePessoas |> Seq.map Dto.OutputPessoaDto.fromDatabaseDto

            let firstPessoa = Seq.tryHead outputPessoas

            match firstPessoa with
            | Some p ->
                let serializedPessoa = serializer.SerializeToString p

                ctx.SetStatusCode 200
                return! text serializedPessoa next ctx
            | None ->
                ctx.SetStatusCode 404
                return! text "Not Found" next ctx
        }

let countPessoasHandler () =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let conn: IDbConnection = Database.getDbConnection ()

            let! (result: Generic.IEnumerable<{| Value: int64 |}>) = Repository.countPessoas conn

            let value = Seq.head result

            ctx.SetStatusCode 200
            return! text (sprintf "%i" value.Value) next ctx
        }
