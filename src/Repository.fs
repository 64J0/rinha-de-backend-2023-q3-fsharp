module Rinha.Repository

open System
open System.Threading.Tasks
open System.Collections.Generic
open Microsoft.Extensions.Logging

open Dapper.FSharp.PostgreSQL

open Rinha
open Npgsql

let personTable: QuerySource<Dto.DatabasePessoaDto> =
    table'<Dto.DatabasePessoaDto> "pessoas"

type CountPessoas = { Value: int64 }

let searchPessoasByT
    (logger: ILogger)
    (conn: NpgsqlConnection)
    (t: string)
    : Task<Result<IEnumerable<Dto.DatabasePessoaDto>, string>> =
    task {
        try
            let pattern = $"%%{t}%%"

            let! result =
                select {
                    for p in personTable do
                        where (like p.apelido pattern)
                        orWhere (like p.nome pattern)
                        orWhere (like p.stack pattern)
                }
                |> conn.SelectAsync<Dto.DatabasePessoaDto>

            return Ok result
        with err ->
            logger.LogCritical($"Error on searchPessoasByT:\n{err}")
            return Error err.Message
    }

let searchPessoaById
    (logger: ILogger)
    (conn: NpgsqlConnection)
    (id: Guid)
    : Task<Result<IEnumerable<Dto.DatabasePessoaDto>, string>> =
    task {
        try
            let! result =
                select {
                    for p in personTable do
                        where (p.id = id)
                }
                |> conn.SelectAsync<Dto.DatabasePessoaDto>

            return Ok result
        with err ->
            logger.LogCritical($"Error on searchPessoaById:\n{err}")
            return Error err.Message
    }

let countPessoas (logger: ILogger) (conn: NpgsqlConnection) : Task<Result<IEnumerable<CountPessoas>, string>> =
    task {
        try
            let! result =
                select {
                    for _p in personTable do
                        count "*" "Value"
                }
                |> conn.SelectAsync<CountPessoas>

            return Ok result
        with err ->
            logger.LogCritical($"Error on countPessoas:\n{err}")
            return Error err.Message
    }
