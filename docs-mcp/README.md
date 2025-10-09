# Duende.Documentation.Mcp.Server

This project contains an MCP server that can extend any large language model that supports MCP with up-to-date knowledge about Duende products.

## MCP Tools

The server has several tools available:
* Free text search on blogs, docs, or samples
* Fetch specific page
* Get all content for a sample
* Get a specific file from a sample

The MCP server has [instructions](src/Duende.Documentation.Mcp.Server/Program.cs) to announce the tools and instruct the LLM to use them.

While the MCP prompt is elaborate, you may need to be explicit in prompts and for example, add "Use Duende samples" when looking for code samples.

## Run and Register the MCP

### Visual Studio Code

Add a `.vscode/mcp.json` to your workspace:

```json
{
  "servers": {
    "duende-mcp-hosted": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Duende.Documentation.Mcp.Server@1.0.0", "--yes"],
      "env": {}
    }
  }
}
```

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

## Indexers

The project uses full-text search with SQLite. There are indexes for docs, blog, and samples. Indexes are built by dedicated background services.

### Docs

Documentation is indexed by parsing LLMs.txt hosted on Duende documentation: https://docs.duendesoftware.com/llms.txt

LLMs.txt is a technique that allows LLMs to find information in a Markdown-based format, so it can be parsed more easily (and within the LLM context).
While available on many websites, none of the vendors currently support this out-of-the-box.

### Blog

Blogs are indexed by parsing the RSS feed of the at https://duendesoftware.com/blog/, and then fetching each page and converting it into markdown.

### Samples

Samples are indexed by looking at the samples-specific LLMs.txt file at https://docs.duendesoftware.com/_llms-txt/identityserver-sample-code.txt

This document contains sample names and descriptions, and includes links to GitHub. The GitHub repository is downloaded as an archive (https://github.com/duendesoftware/samples/archive/refs/heads/main.zip), and as part of indexing all of the `.cs`, `.cshtml` and relevant `.js` files are added to the index. Other files are ignored.
