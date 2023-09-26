namespace Rinha

open System
open FsToolkit.ErrorHandling

// Inspired by Serialization (chapter 11) from "Domain Modeling Made Functional"

module Domain =

    type ValidNome = private ValidNome of string
    type ValidApelido = private ValidApelido of string
    type ValidStack = private ValidStack of string list

    module ValidNome =
        let value (ValidNome nome) : string = nome

        let create (nome: string) : Result<ValidNome, string> =
            if String.IsNullOrEmpty nome then
                Error "Name should not be null or empty"
            elif nome.Length > 100 then
                Error "Name should have less than 100 characters"
            else
                Ok(ValidNome nome)

    module ValidApelido =
        let value (ValidApelido apelido) = apelido

        let create (apelido: string) : Result<ValidApelido, string> =
            if String.IsNullOrEmpty apelido then
                Error "Apelido should not be null or empty"
            elif apelido.Length > 32 then
                Error "Apelido should not have more than 32 characters"
            else
                Ok(ValidApelido apelido)

    module ValidStack =
        let value (ValidStack stack) = stack

        let create (stack: string list) : Result<ValidStack, string> =
            let invalidStackItemLen: bool =
                stack
                |> List.exists (fun (item: string) -> (item.Length > 32) || (item.Length = 0))

            match invalidStackItemLen with
            | true -> Error "Invalid stack length."
            | false -> Ok(ValidStack stack)

    type Pessoa =
        { Id: Guid
          Apelido: ValidApelido
          Nome: ValidNome
          Nascimento: DateTime
          Stack: ValidStack }

module Dto =

    type InputPessoaDto =
        { apelido: string
          nome: string
          nascimento: DateOnly
          stack: string list }

    module InputPessoaDto =

        let toDomain (dto: InputPessoaDto) : Result<Domain.Pessoa, string> =
            result {
                let! nome = dto.nome |> Domain.ValidNome.create
                let! apelido = dto.apelido |> Domain.ValidApelido.create
                let! stack = dto.stack |> Domain.ValidStack.create
                let id = Guid.NewGuid()
                let nascimento = dto.nascimento |> string |> DateTime.Parse

                return
                    { Id = id
                      Apelido = apelido
                      Nome = nome
                      Nascimento = nascimento
                      Stack = stack }
            }

    // Must be lowercase due to:
    // https://github.com/Dzoukr/Dapper.FSharp#how-does-the-library-works
    [<CLIMutable>]
    type DatabasePessoaDto =
        { id: Guid
          apelido: string
          nome: string
          nascimento: DateTime
          stack: string }

    module DatabasePessoaDto =

        let fromDomain (pessoa: Domain.Pessoa) : DatabasePessoaDto =
            let id = pessoa.Id
            let nome = pessoa.Nome |> Domain.ValidNome.value
            let apelido = pessoa.Apelido |> Domain.ValidApelido.value
            let stack = pessoa.Stack |> Domain.ValidStack.value
            let nascimento = pessoa.Nascimento

            { id = id
              apelido = apelido
              nome = nome
              nascimento = nascimento
              stack = stack.ToString() }

    type OutputPessoaDto =
        { id: Guid
          apelido: string
          nome: string
          nascimento: DateOnly
          stack: string }

    module OutputPessoaDto =

        let fromDatabaseDto (pessoa: DatabasePessoaDto) : OutputPessoaDto =
            let id = pessoa.id
            let nome = pessoa.nome
            let apelido = pessoa.apelido
            let stack = pessoa.stack
            let nascimento = pessoa.nascimento |> DateOnly.FromDateTime

            { id = id
              apelido = apelido
              nome = nome
              nascimento = nascimento
              stack = stack.ToString() }
