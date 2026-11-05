// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.LanguageServer.Protocol;

var connection = new JsonRpcConnection(Console.OpenStandardInput(), Console.OpenStandardOutput());
using var server = new LspServer(connection);
await server.RunAsync();
