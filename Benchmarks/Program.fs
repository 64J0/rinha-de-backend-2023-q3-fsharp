module BenchmarkRinha

open System
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.AspNetCore.Http
open System.Collections.Concurrent

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open Rinha

type Serialization() =
    let _pessoa: Dto.OutputPessoaDto =
        { id = Guid.NewGuid()
          apelido = "joaozinho2321"
          nome = "Joao dos Santos"
          nascimento = DateOnly.Parse("2023-07-07")
          stack = [| "C#"; "Go"; "Java"; "Python"; "Rust" |] }

    [<Benchmark(Baseline = true)>]
    member _.SerializeDefault() : string =
        let jsonOptions = JsonFSharpOptions.Default().ToJsonSerializerOptions()
        JsonSerializer.Serialize(_pessoa, jsonOptions)

    [<Benchmark>]
    member _.SerializeFSharpLuLike() : string =
        let jsonOptions = JsonFSharpOptions.FSharpLuLike().ToJsonSerializerOptions()
        JsonSerializer.Serialize(_pessoa, jsonOptions)

    [<Benchmark>]
    member _.SerializeInheritUnionEncoding() : string =
        let jsonOptions = JsonFSharpOptions.InheritUnionEncoding().ToJsonSerializerOptions()
        JsonSerializer.Serialize(_pessoa, jsonOptions)

    [<Benchmark>]
    member _.SerializeNewtonsoftLike() : string =
        let jsonOptions = JsonFSharpOptions.NewtonsoftLike().ToJsonSerializerOptions()
        JsonSerializer.Serialize(_pessoa, jsonOptions)

    [<Benchmark>]
    member _.SerializeThothLike() : string =
        let jsonOptions = JsonFSharpOptions.ThothLike().ToJsonSerializerOptions()
        JsonSerializer.Serialize(_pessoa, jsonOptions)


// type SearchEndpoint() = ()


[<EntryPoint>]
let main (_args: string[]) : int =
    BenchmarkRunner.Run<Serialization>() |> ignore
    0
