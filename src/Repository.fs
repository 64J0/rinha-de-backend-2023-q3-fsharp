module Rinha.Repository

open System
open System.Data
open System.Threading.Tasks
open Dapper.FSharp.PostgreSQL

open Rinha

let personTable: QuerySource<Dto.OutputPessoaDto> =
    table'<Dto.OutputPessoaDto> "pessoas"

let insertPessoa (conn: IDbConnection) (pessoa: Dto.OutputPessoaDto) : Task<int> =
    insert {
        into personTable
        value pessoa
    }
    |> conn.InsertAsync

let searchPessoas (conn: IDbConnection) =
    select {
        for _p in personTable do
            take 50
    }
    |> conn.SelectAsync<Dto.OutputPessoaDto>

let searchPessoaById (conn: IDbConnection) (id: Guid) =
    select {
        for p in personTable do
            where (p.id = id)
    }
    |> conn.SelectAsync<Dto.OutputPessoaDto>

let countPessoas (conn: IDbConnection) =
    select {
        for _p in personTable do
            count "*" "Value"
    }
    |> conn.SelectAsync<{| Value: int |}>
