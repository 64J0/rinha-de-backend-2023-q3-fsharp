module Rinha.Handlers

open System
open System.Net
open System.Data
open System.Threading.Tasks
open System.Threading.Channels
open System.Collections.Concurrent
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging

open FsToolkit.ErrorHandling
open Giraffe

open Rinha

// TODO use more descriptive names
// Those are services added to the server as Singletons
type IBuscaMap = ConcurrentDictionary<string, Dto.OutputPessoaDto>
type IPessoasById = ConcurrentDictionary<Guid, Dto.OutputPessoaDto>
type IChannelPessoa = Channel<Dto.OutputPessoaDto>
type IApelidoPessoas = ConcurrentDictionary<string, byte>

let createPessoaHandler () =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let conn: IDbConnection = Database.getDbConnection ()
        let logger: ILogger = ctx.GetLogger()
        let serializer: Json.ISerializer = ctx.GetJsonSerializer()
        let apelidoPessoas: IApelidoPessoas = ctx.GetService<IApelidoPessoas>()

        use _ = logger.BeginScope("CreatePessoaHandler")

        let checkDuplicatedApelido
            (apelidoPessoas: ConcurrentDictionary<string, byte>)
            (inputPessoaResult: Result<Dto.InputPessoaDto, string>)
            : Result<Dto.InputPessoaDto, string> =
            match inputPessoaResult with
            | Ok inputPessoa ->
                match apelidoPessoas.TryAdd(inputPessoa.apelido, byte 0) with
                | true -> Ok inputPessoa
                | false ->
                    logger.LogError "[ERROR] checkDuplicatedApelido"
                    ctx.SetStatusCode(int HttpStatusCode.BadRequest)
                    Error "Duplicated apelido"
            | Error err -> Error err

        let createDomainPessoa (inputPessoaResult: Result<Dto.InputPessoaDto, string>) : Result<Domain.Pessoa, string> =
            match inputPessoaResult with
            | Ok inputPessoa ->
                let domainPessoaResult = Dto.InputPessoaDto.toDomain inputPessoa

                match domainPessoaResult with
                | Ok domainPessoa -> Ok domainPessoa
                | Error err ->
                    logger.LogError(sprintf "[ERROR] createDomainPessoa: %A" err)
                    ctx.SetStatusCode(int HttpStatusCode.UnprocessableEntity)
                    Error "Domain error"
            | Error err -> Error err


        let storePessoaOnDatabase
            (logger: ILogger)
            (conn: IDbConnection)
            (serializer: Json.ISerializer)
            (domainPessoaResult: Result<Domain.Pessoa, string>)
            : Task<Result<int, string>> =
            task {
                match domainPessoaResult with
                | Ok domainPessoa ->
                    let databasePessoa =
                        Dto.DatabasePessoaDto.fromDomain (serializer.SerializeToString) (domainPessoa)

                    let! databaseResult = Repository.insertPessoa logger conn databasePessoa

                    match databaseResult with
                    | Ok dbVal -> return Ok dbVal
                    | Error err ->
                        logger.LogError(sprintf "[ERROR] storePessoaOnDatabase: %A" err)
                        ctx.SetStatusCode(int HttpStatusCode.InternalServerError)
                        return Error "Error when storing Pessoa on database"
                | Error err -> return Error err
            }

        task {
            let! input = ctx.ReadBodyBufferedFromRequestAsync()
            let inputPessoa = serializer.Deserialize<Dto.InputPessoaDto> input

            let! result =
                Ok inputPessoa
                |> checkDuplicatedApelido apelidoPessoas
                |> createDomainPessoa
                |> storePessoaOnDatabase logger conn serializer

            match result with
            | Ok dbVal ->
                ctx.SetStatusCode(int HttpStatusCode.Created)
                return! text $"Pessoa inserted, return code {dbVal}" next ctx
            | Error err -> return! text err next ctx
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
