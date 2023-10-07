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
open NATS.Client.Core
open Giraffe

open Rinha

// TODO use more descriptive names
// Those are services added to the server as Singletons
type IBuscaMap = ConcurrentDictionary<string, Dto.OutputPessoaDto>
type IPessoasById = ConcurrentDictionary<Guid, Dto.DatabasePessoaDto>
type IChannelPessoa = Channel<Dto.DatabasePessoaDto>
type IApelidoPessoas = ConcurrentDictionary<string, byte>
type INatsOwnChannel = string

let createPessoaHandler () =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let conn: IDbConnection = Database.getDbConnection ()
        let logger: ILogger = ctx.GetLogger()
        let serializer: Json.ISerializer = ctx.GetJsonSerializer()
        let apelidoPessoas: IApelidoPessoas = ctx.GetService<IApelidoPessoas>()
        let natsConnection: INatsConnection = ctx.GetService<INatsConnection>()
        let channel: IChannelPessoa = ctx.GetService<IChannelPessoa>()
        let pessoasById: IPessoasById = ctx.GetService<IPessoasById>()

        use _ = logger.BeginScope("CreatePessoaHandler")

        let checkDuplicatedApelido
            (apelidoPessoas: IApelidoPessoas)
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

        let getDatabasePessoaDto
            (serializer: Json.ISerializer)
            (domainPessoaResult: Result<Domain.Pessoa, string>)
            : Result<Dto.DatabasePessoaDto, string> =
            match domainPessoaResult with
            | Ok domainPessoa ->
                let databasePessoa =
                    Dto.DatabasePessoaDto.fromDomain (serializer.SerializeToString) (domainPessoa)

                Ok databasePessoa
            | Error err -> Error err

        let storePessoaOnNatsCache
            (natsConnection: INatsConnection)
            (databasePessoaDtoResult: Result<Dto.DatabasePessoaDto, string>)
            : Task<Result<Dto.DatabasePessoaDto, string>> =
            task {
                match databasePessoaDtoResult with
                | Ok databasePessoaDto ->
                    // TODO improve error handling
                    do! natsConnection.PublishAsync(Rinha.Environment.NATS_DESTINATION, databasePessoaDto)

                    return Ok databasePessoaDto
                | Error err -> return Error err
            }

        let writeToChannel
            (channel: IChannelPessoa)
            (databasePessoaDtoResultTask: Task<Result<Dto.DatabasePessoaDto, string>>)
            : Task<Result<Dto.DatabasePessoaDto, string>> =
            task {
                let! databasePessoaDtoResult = databasePessoaDtoResultTask

                match databasePessoaDtoResult with
                | Ok databasePessoaDto ->
                    // TODO improve error handling
                    do! channel.Writer.WriteAsync(databasePessoaDto)

                    return Ok databasePessoaDto
                | Error err -> return Error err
            }

        let storeOnPessoasById
            (pessoasById: IPessoasById)
            (databasePessoaDtoResultTask: Task<Result<Dto.DatabasePessoaDto, string>>)
            : Task<Result<Dto.DatabasePessoaDto, string>> =
            task {
                let! databasePessoaDtoResult = databasePessoaDtoResultTask

                match databasePessoaDtoResult with
                | Ok databasePessoaDto ->
                    match pessoasById.TryAdd(databasePessoaDto.id, databasePessoaDto) with
                    | true -> return Ok databasePessoaDto
                    | false ->
                        logger.LogError "[ERROR] storeOnPessoasById"
                        ctx.SetStatusCode(int HttpStatusCode.BadRequest)
                        return Error "Failed to store on ConcurrentDictionary pessoasById"
                | Error err -> return Error err
            }

        let storePessoaOnDatabase
            (logger: ILogger)
            (conn: IDbConnection)
            (databasePessoaDtoResultTask: Task<Result<Dto.DatabasePessoaDto, string>>)
            : Task<Result<int, string>> =
            task {
                let! databasePessoaDtoResult = databasePessoaDtoResultTask

                match databasePessoaDtoResult with
                | Ok databasePessoaDto ->
                    let! databaseResult = Repository.insertPessoa logger conn databasePessoaDto

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
                |> getDatabasePessoaDto serializer
                |> storePessoaOnNatsCache natsConnection
                |> writeToChannel channel
                |> storeOnPessoasById pessoasById
                |> storePessoaOnDatabase logger conn

            match result with
            | Ok dbVal ->
                ctx.SetStatusCode(int HttpStatusCode.Created)
                return! text $"Pessoa inserted, return code {dbVal}" next ctx
            | Error err -> return! text err next ctx
        }

let searchPessoasByTHandler () =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let conn: IDbConnection = Database.getDbConnection ()
        let logger: ILogger = ctx.GetLogger()
        let serializer: Json.ISerializer = ctx.GetJsonSerializer()

        use _ = logger.BeginScope("SearchPessoasByTHandler")

        task {
            let term = ctx.TryGetQueryStringValue "t"

            match term with
            | Some t ->
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
        let conn: IDbConnection = Database.getDbConnection ()
        let logger: ILogger = ctx.GetLogger()
        let serializer: Json.ISerializer = ctx.GetJsonSerializer()
        let pessoasById: IPessoasById = ctx.GetService<IPessoasById>()

        use _ = logger.BeginScope("SearchPessoaByIdHandler")

        let tryParseToGuid (id: string) : Option<Guid> =
            match Guid.TryParse id with
            | true, guid -> Some guid
            | false, _ -> None

        let tryReadPessoaFromCache
            (cache: IPessoasById)
            (guid: Guid)
            (pessoa: Task<Option<Dto.DatabasePessoaDto>>)
            : Task<Option<Dto.DatabasePessoaDto>> =
            task {
                let! pessoa' = pessoa

                match pessoa' with
                | Some p -> return Some p
                | None ->
                    match cache.TryGetValue guid with
                    | true, p' -> return Some p'
                    | false, _ -> return None
            }

        let waitTenSeconds (pessoa: Task<Option<Dto.DatabasePessoaDto>>) : Task<Option<Dto.DatabasePessoaDto>> =
            task {
                let! pessoa' = pessoa

                match pessoa' with
                | Some p -> return Some p
                | None ->
                    do! Task.Delay 10
                    return None
            }

        task {
            let id = input
            let parsedGuid = tryParseToGuid id

            // TODO improve code to avoid nested structures
            match parsedGuid with
            | Some guid ->
                let! result =
                    task { return None }
                    |> tryReadPessoaFromCache pessoasById guid
                    |> waitTenSeconds
                    |> tryReadPessoaFromCache pessoasById guid
                    |> waitTenSeconds
                    |> tryReadPessoaFromCache pessoasById guid

                match result with
                | Some pessoa ->
                    let outputPessoa =
                        pessoa |> Dto.OutputPessoaDto.fromDatabaseDto serializer.Deserialize

                    let serializedPessoa = serializer.SerializeToString outputPessoa

                    ctx.SetStatusCode(int HttpStatusCode.OK)
                    return! text serializedPessoa next ctx
                | None ->
                    let! databasePessoas = Repository.searchPessoaById logger conn guid

                    match databasePessoas with
                    | Ok pessoas ->
                        let outputPessoas =
                            pessoas |> Seq.map (Dto.OutputPessoaDto.fromDatabaseDto serializer.Deserialize)

                        let firstPessoa = Seq.tryHead outputPessoas

                        match firstPessoa with
                        | Some p ->
                            let serializedPessoa = serializer.SerializeToString p

                            ctx.SetStatusCode(int HttpStatusCode.OK)
                            return! text serializedPessoa next ctx
                        | None ->
                            ctx.SetStatusCode(int HttpStatusCode.NotFound)
                            return! text "Not Found" next ctx
                    | Error err ->
                        ctx.SetStatusCode(int HttpStatusCode.InternalServerError)
                        return! text err next ctx
            | None ->
                ctx.SetStatusCode(int HttpStatusCode.BadRequest)
                return! text "Invalid Guid!" next ctx
        }

let countPessoasHandler () =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let conn: IDbConnection = Database.getDbConnection ()
        let logger: ILogger = ctx.GetLogger()

        use _ = logger.BeginScope("CountPessoasHandler")

        task {
            let! databaseResult = Repository.countPessoas logger conn

            match databaseResult with
            | Ok result ->
                let value = Seq.head result

                ctx.SetStatusCode(int HttpStatusCode.OK)
                return! text (sprintf "%i" value.Value) next ctx
            | Error err ->
                ctx.SetStatusCode(int HttpStatusCode.InternalServerError)
                return! text err next ctx
        }
