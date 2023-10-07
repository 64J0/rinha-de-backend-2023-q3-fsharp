module Rinha.Environment

open System

// TODO add validation
let DB_CONN = Environment.GetEnvironmentVariable "DB_CONNECTION_STRING"
let NATS_URL = Environment.GetEnvironmentVariable "NATS_URL"
let NATS_DESTINATION = Environment.GetEnvironmentVariable "NATS_DESTINATION"
let NATS_OWN_CHANNEL = Environment.GetEnvironmentVariable "NATS_OWN_CHANNEL"
