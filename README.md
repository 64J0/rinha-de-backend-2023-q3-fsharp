# Rinha de backend Q3 - F#

Esse projeto consiste na tradução da solução em C# disponível [neste link](https://github.com/andr3marra/rinha-de-backend-2023-q3-csharp). Note que optei por não fazer a tradução ao pé da letra, pois acredito que alguns pontos deveriam ser melhor implementados.

As instruções originais do desafio podem ser encontradas [aqui](https://github.com/zanfranceschi/rinha-de-backend-2023-q3/blob/main/INSTRUCOES.md).

## Pacotes

* [Fantomas](https://github.com/fsprojects/fantomas): Formatação do código;
* [Npgsql](https://github.com/npgsql/npgsql): Provedor de dados em .NET para o PostgreSQL (banco de dados da solução);
* [Dapper.FSharp](https://github.com/Dzoukr/Dapper.FSharp): Extensão leve em F# para o [Dapper](https://github.com/DapperLib/Dapper) (ORM) em C#;
* [Giraffe](https://github.com/giraffe-fsharp/Giraffe): Um framework funcional nativo para desenvolvimento Web com o ASP.NET Core em F#;
* [FSharp.SystemTextJson](https://github.com/Tarmil/FSharp.SystemTextJson): Essa biblioteca adicionar suporte para os tipos em F# no [System.Text.Json](https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-apis/). É usada no projeto para a (de)serialização de JSON;
* [FsToolkit.ErrorHandling](https://github.com/demystifyfp/FsToolkit.ErrorHandling): Biblioteca com várias funções que ajudam no tratamento de erros.
* [NATS.Client.Hosting](https://github.com/nats-io/nats.net.v2): Cliente assíncrono em C#/.NET para o [NATS](https://docs.nats.io/nats-concepts/overview).

## Rodando o projeto principal no ambiente local

```bash
cd participacao/
docker-compose up -d
```

## Rodando o benchmark

```bash
cd Benchmarks/
dotnet run -c Release
```