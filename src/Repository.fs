module Rinha.Repository

open System
open System.Data
open System.Threading.Tasks
open Dapper.FSharp.PostgreSQL

open Rinha

let personTable: QuerySource<Dto.DatabasePessoaDto> =
    table'<Dto.DatabasePessoaDto> "pessoas"

let insertPessoa (conn: IDbConnection) (pessoa: Dto.DatabasePessoaDto) : Task<int> =
    insert {
        into personTable
        value pessoa
    }
    |> conn.InsertAsync

let searchPessoasByT (conn: IDbConnection) (t: string) =
    let pattern = $"%%{t}%%"

    select {
        for p in personTable do
            where (like p.apelido pattern)
            orWhere (like p.nome pattern)
            orWhere (like p.stack pattern)
    }
    |> conn.SelectAsync<Dto.DatabasePessoaDto>

let searchPessoaById (conn: IDbConnection) (id: Guid) =
    select {
        for p in personTable do
            where (p.id = id)
    }
    |> conn.SelectAsync<Dto.DatabasePessoaDto>

let countPessoas (conn: IDbConnection) =
    select {
        for _p in personTable do
            count "*" "Value"
    }
    |> conn.SelectAsync<{| Value: int64 |}>
