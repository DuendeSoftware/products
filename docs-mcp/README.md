# Duende Documentation MCP Server

MCP is an open protocol that enables AI models to securely interact with local and remote resources through standardized
server implementations. This project contains an MCP server that can extend any large language model that supports MCP
with up-to-date knowledge about Duende products, sourced from [documentation](https://docs.duendesoftware.com/),
[blog](https://duendesoftware.com/blog/) and [samples](https://github.com/duendesoftware/samples).

## Register the MCP

To run the Duende Documentation MCP Server, you will need the `dnx` tool (included in the .NET 10 SDK) in your
system's `PATH`. The `dnx` tool can download and run applications packaged and distributed through NuGet.

You can then register the MCP server in your tool of choice. While the exact steps may vary depending on your IDE,
here are some common configurations.

### Visual Studio / Visual Studio Code

You can register the Duende Documentation MCP Server
[in your user settings](https://code.visualstudio.com/docs/copilot/chat/mcp-servers#_add-an-mcp-server-to-your-user-settings)
to make it available in any workspace.

Alternatively, you can add a `.vscode/mcp.json` file to your workspace:

```json
{
  "servers": {
    "duende-mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Duende.Documentation.Mcp", "--yes"],
      "env": {}
    }
  }
}
```

You can also add the `--database` parameter with a path to the database file to use.
The database file will be created by the MCP server at the specified path:

```json
{
  "servers": {
    "duende-mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Duende.Documentation.Mcp", "--yes", "--", "--database", "/path/to/database.db"],
      "env": {}
    }
  }
}
```

Open GitHub Copilot and select Agent Mode to work with the MCP server.

### JetBrains Rider

In JetBrains Rider settings, navigate to **Tools \| AI Assistant \| Model Context Protocol (MCP)**.
Next, add a new MCP server. In the dialog that opens, select **As JSON** and enter the following configuration:

```json
{
  "mcpServers": {
    "duende-mcp": {
      "command": "dnx",
      "args": ["Duende.Documentation.Mcp", "--yes"]
    }
  }
}
```

Alternatively, you can add the `--database` parameter with a path to the database file to use.
The database file will be created by the MCP server at the specified path:

```json
{
  "mcpServers": {
    "duende-mcp": {
      "command": "dnx",
      "args": ["Duende.Documentation.Mcp", "--yes", "--", "--database", "/path/to/database.db"]
    }
  }
}
```

### Claude Code

Execute the following command:

```shell
claude mcp add --transport stdio duende-mcp -- dnx Duende.Documentation.Mcp --yes
```

Set the working directory to a path on your machine where the Duende Documentation MCP Server can store its database
index. Not setting the working directory will result in the MCP server failing to start because it cannot create the
database file.

Alternatively, you can add the `--database` parameter with a path to a database file that the MCP server has write access to.
The file will be created and mnaaged by the MCP server.

## Tools and Example Prompts

The Duende Documentation MCP Server has several tools available:

* Free text search on blogs, docs, or samples
* Fetch specific page
* Get all content for a sample
* Get a specific file from a sample

The Duende Documentation MCP Server has [instructions](src/Documentation.Mcp/Program.cs) to announce the
tools it provides, and instructs the LLM to use them. While this MCP prompt is elaborate, you may need to be explicit
in prompts and for example, add "Use Duende samples" when you expect to update code with your LLM.

Example prompts:

* "What is a client in OpenID Connect?"
* "What is automatic key management?"
* "How can I validate a JWT token in ASP.NET Core?"
* "What is a Personal Access Token and how do I create one?"

Sometimes, it may be necessary to provide more context to the LLM. For example, when you want to know more about a
specific topic you expect in the Duende documentation or blog, you can instruct the LLM to use the Duende Documentation
MCP Server:

* "Explain .NET TLS certificates - use Duende"
* "Can I add passkeys to Razor Pages? use Duende"

## Support

If you experience an issue with the Duende Documentation MCP or have any other feedback, please open an issue in our
[Duende community](https://duende.link/community).

## Technical details

### Development

* Run the project. This will host a server on port 3000 (http), and with stdio bindings.
* In VS Code, add a `.vscode/mcp.json` to your workspace:
  ```json
  {
    "servers": {
      "duende-mcp": {
        "type": "http",
        "url": "http://localhost:3000"
      }
    }
  }
  ```

### Indexers

The project uses full-text search with SQLite. There are indexes for docs, blog, and samples. Indexes are built by
dedicated background services.

#### Docs

Documentation is indexed by parsing LLMs.txt hosted on Duende documentation: https://docs.duendesoftware.com/llms.txt

LLMs.txt is a technique that allows LLMs to find information in a Markdown-based format, so it can be parsed more
easily (and within the LLM context). While available on many websites, none of the vendors currently support this
out-of-the-box.

#### Blog

Blogs are indexed by parsing the RSS feed of the at https://duendesoftware.com/blog/, and then fetching each page and
converting it into markdown.

#### Samples

Samples are indexed by looking at the samples-specific LLMs.txt file
at https://docs.duendesoftware.com/_llms-txt/identityserver-sample-code.txt

This document contains sample names and descriptions, and includes links to GitHub. The GitHub repository is downloaded
[as an archive](https://github.com/duendesoftware/samples/archive/refs/heads/main.zip), and all `.cs`, `.cshtml`
and relevant `.js` files are added to the index. Other files are ignored.
