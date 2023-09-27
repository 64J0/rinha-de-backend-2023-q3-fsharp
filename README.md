:warning: Work in progress! :warning:

# Rinha de backend Q3 - F#

Esse projeto consiste na tradução da solução em C# disponível [neste link](https://github.com/andr3marra/rinha-de-backend-2023-q3-csharp).

As instruções originais do desafio podem ser encontradas [aqui](https://github.com/zanfranceschi/rinha-de-backend-2023-q3/blob/main/INSTRUCOES.md).

## Pacotes

* [Fantomas](https://github.com/fsprojects/fantomas): Formatação do código;
* [Npgsql](https://github.com/npgsql/npgsql): Provedor de dados em .NET para o PostgreSQL (banco de dados da solução);
* [Dapper.FSharp](https://github.com/Dzoukr/Dapper.FSharp): Extensão leve em F# para o [Dapper](https://github.com/DapperLib/Dapper) (ORM) em C#;
* [Giraffe](https://github.com/giraffe-fsharp/Giraffe): Um framework funcional nativo para desenvolvimento Web com o ASP.NET Core em F#;
* [FSharp.SystemTextJson](https://github.com/Tarmil/FSharp.SystemTextJson): Essa biblioteca adicionar suporte para os tipos em F# no [System.Text.Json](https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-apis/). É usada no projeto para a (de)serialização de JSON;
* [FsToolkit.ErrorHandling](https://github.com/demystifyfp/FsToolkit.ErrorHandling): Biblioteca com várias funções que ajudam no tratamento de erros.

## Rodando local

```bash
cd participacao/
docker-compose up -d db

export DB_CONNECTION_STRING="Host=localhost;Username=admin;Password=123;Database=rinha;Connection Pruning Interval=1;Connection Idle Lifetime=2;Enlist=false;No Reset On Close=true"
# start the server on debug mode to check the values
```