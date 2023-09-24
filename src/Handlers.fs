module Rinha.Handlers

open System.Data
open Microsoft.AspNetCore.Http
open Giraffe

open Rinha

let createPessoaHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let conn: IDbConnection = Database.getDbConnection ()

            let serializer = ctx.GetJsonSerializer()
            let! input = ctx.ReadBodyBufferedFromRequestAsync()
            let inputPessoa = serializer.Deserialize<Dto.InputPessoaDto> input

            let domainPessoa = Dto.InputPessoaDto.toDomain inputPessoa

            match domainPessoa with
            | Ok pessoa ->
                let outputPessoa = Dto.OutputPessoaDto.fromDomain pessoa

                let! result = Repository.insertPessoa conn outputPessoa
                ctx.SetStatusCode 201
                return! text $"Pessoa inserted, return code {result}" next ctx
            | Error err ->
                ctx.SetStatusCode 400
                return! text $"Error when inserting pessoa: {err}" next ctx
        }

let readPessoasHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let conn: IDbConnection = Database.getDbConnection ()
            let serializer = ctx.GetJsonSerializer()

            let! (pessoas: System.Collections.Generic.IEnumerable<Dto.OutputPessoaDto>) = Repository.searchPessoas conn

            let serializedPessoas = serializer.SerializeToString pessoas

            ctx.SetStatusCode 200
            return! text serializedPessoas next ctx
        }
