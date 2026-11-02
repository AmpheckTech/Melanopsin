import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import * as vscode from 'vscode';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  State,
  TransportKind,
} from 'vscode-languageclient/node';

const languageId = 'melanopsin-mxml';
const configurationSection = 'melanopsin.mxml';
const serverDll = path.join(
  'build',
  'bin',
  'Melanopsin.UI.LanguageServer',
  'debug',
  'Melanopsin.UI.LanguageServer.dll',
);
const installedServerDll = path.join(
  os.homedir(),
  '.local',
  'share',
  'melanopsin',
  'language-server',
  serverDll,
);

let client: LanguageClient | undefined;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const output = vscode.window.createOutputChannel('Melanopsin MXML Language Server');
  context.subscriptions.push(output);
  context.subscriptions.push(
    vscode.commands.registerCommand(
      'melanopsin.mxml.restartLanguageServer',
      async () => {
        await stopClient();
        await startClient(context, output);
      },
    ),
  );
  context.subscriptions.push(
    vscode.commands.registerCommand(
      'melanopsin.mxml.goToMatchingTag',
      async (uri: string, position: { line: number; character: number }) => {
        const document = await vscode.workspace.openTextDocument(vscode.Uri.parse(uri));
        const editor = await vscode.window.showTextDocument(document);
        const target = new vscode.Position(position.line, position.character);
        editor.selection = new vscode.Selection(target, target);
        editor.revealRange(
          new vscode.Range(target, target),
          vscode.TextEditorRevealType.InCenterIfOutsideViewport,
        );
      },
    ),
  );

  await startClient(context, output);
}

export async function deactivate(): Promise<void> {
  await stopClient();
}

async function startClient(
  context: vscode.ExtensionContext,
  output: vscode.OutputChannel,
): Promise<void> {
  const serverPath = resolveServerPath(context.extensionUri);
  if (!serverPath) {
    const message =
      'Melanopsin MXML language server was not found. Build the solution, or set ' +
      'melanopsin.mxml.server.path to the absolute path of ' +
      'Melanopsin.UI.LanguageServer.dll.';
    output.appendLine(message);
    void vscode.window.showErrorMessage(message);
    return;
  }

  const launch = serverLaunch(serverPath);
  const serverOptions: ServerOptions = {
    run: {
      command: launch.command,
      args: launch.args,
      transport: TransportKind.stdio,
      options: { cwd: path.dirname(serverPath) },
    },
    debug: {
      command: launch.command,
      args: launch.args,
      transport: TransportKind.stdio,
      options: { cwd: path.dirname(serverPath) },
    },
  };
  const clientOptions: LanguageClientOptions = {
    documentSelector: [
      { scheme: 'file', language: languageId },
      { scheme: 'untitled', language: languageId },
    ],
    diagnosticCollectionName: 'mxml',
    outputChannel: output,
    synchronize: {
      fileEvents: [
        vscode.workspace.createFileSystemWatcher('**/*.{cs,csproj}'),
      ],
    },
  };

  output.appendLine(`Starting language server: ${serverPath}`);
  client = new LanguageClient(
    'melanopsinMxml',
    'Melanopsin MXML',
    serverOptions,
    clientOptions,
  );
  client.onDidChangeState(({ newState }) => {
    const stateName = State[newState] ?? String(newState);
    output.appendLine(`Language server state: ${stateName}.`);
  });
  try {
    await client.start();
    output.appendLine('Language server is ready.');
  } catch (error) {
    output.appendLine(String(error));
    client = undefined;
    void vscode.window.showErrorMessage(
      `Melanopsin MXML language server failed to start: ${String(error)}`,
    );
    return;
  }

  try {
    await preloadMarkupProjects(client);
  } catch (error) {
    output.appendLine(`Project preload skipped: ${String(error)}`);
  }
}

async function preloadMarkupProjects(running: LanguageClient): Promise<void> {
  const views = await vscode.workspace.findFiles(
    '**/*.mxml',
    '**/{.git,bin,obj,node_modules}/**',
    32,
  );
  if (views.length === 0) {
    return;
  }

  await running.sendNotification('melanopsin/preload', {
    paths: [...new Set(views.map((view) => view.fsPath))],
  });
}

async function stopClient(): Promise<void> {
  const running = client;
  client = undefined;
  if (running) {
    await running.stop();
  }
}

function resolveServerPath(extensionUri: vscode.Uri): string | undefined {
  const configured = vscode.workspace
    .getConfiguration(configurationSection)
    .get<string>('server.path')
    ?.trim();
  const candidate = configured || process.env.MELANOPSIN_MXML_SERVER?.trim();
  if (candidate) {
    return existingFile(expandWorkspaceFolder(candidate));
  }

  for (const root of discoveryRoots(extensionUri)) {
    for (const directory of ancestors(root)) {
      const found = existingFile(path.join(directory, serverDll));
      if (found) {
        return found;
      }
    }
  }

  return existingFile(installedServerDll);
}

function discoveryRoots(extensionUri: vscode.Uri): Set<string> {
  const roots = new Set<string>();
  for (const folder of vscode.workspace.workspaceFolders ?? []) {
    roots.add(folder.uri.fsPath);
  }

  const visibleDocuments = [
    vscode.window.activeTextEditor?.document,
    ...vscode.window.visibleTextEditors.map((editor) => editor.document),
  ];
  for (const document of visibleDocuments) {
    if (document?.uri.scheme === 'file') {
      roots.add(path.dirname(document.uri.fsPath));
    }
  }
  for (const document of vscode.workspace.textDocuments) {
    if (document.uri.scheme === 'file' && document.languageId === languageId) {
      roots.add(path.dirname(document.uri.fsPath));
    }
  }

  if (extensionUri.scheme === 'file') {
    roots.add(extensionUri.fsPath);
  }
  return roots;
}

function* ancestors(start: string): IterableIterator<string> {
  let current = path.resolve(start);
  while (true) {
    yield current;
    const parent = path.dirname(current);
    if (parent === current) {
      return;
    }
    current = parent;
  }
}

function expandWorkspaceFolder(value: string): string {
  const root = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
  return root
    ? value.replace(/\$\{workspaceFolder\}/g, root)
    : value;
}

function existingFile(value: string): string | undefined {
  const resolved = path.resolve(value);
  try {
    return fs.statSync(resolved).isFile() ? resolved : undefined;
  } catch {
    return undefined;
  }
}

function serverLaunch(serverPath: string): { command: string; args: string[] } {
  return path.extname(serverPath).toLowerCase() === '.dll'
    ? { command: 'dotnet', args: [serverPath] }
    : { command: serverPath, args: [] };
}
