// https://fsharp.org/guides/data-access/
// https://nozzlegear.com/blog/using-dapper-with-fsharp
// https://www.compositional-it.com/news-blog/mirco-orms-and-f/

module Rinha.Database

open System.Data
open Dapper.FSharp
open Npgsql

// So Dapper interprets NULL as Option.None
PostgreSQL.OptionTypes.register ()

let getDbConnection () : IDbConnection =
    let connString: string = Rinha.Environment.DB_CONN
    new NpgsqlConnection(connString) :> IDbConnection
