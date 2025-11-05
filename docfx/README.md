# Orleans.StateMachineES Documentation

This directory contains the DocFx-based documentation for Orleans.StateMachineES, including API reference and comprehensive guides.

## Building Documentation Locally

### Prerequisites

- .NET 9.0 SDK
- DocFx tool

### Install DocFx

```bash
dotnet tool install -g docfx
```

### Build Documentation

```bash
cd docfx
docfx docfx.json
```

The generated documentation will be in `_site/` directory.

### Preview Documentation

```bash
docfx docfx.json --serve
```

Then open http://localhost:8080 in your browser.

## Documentation Structure

```
docfx/
├── docfx.json              # Main configuration
├── filterConfig.yml        # API filtering rules
├── toc.yml                 # Main table of contents
├── index.md                # Landing page
├── api/                    # Auto-generated API docs
│   └── index.md
├── articles/               # Guides and tutorials
│   ├── getting-started/
│   │   ├── index.md
│   │   ├── installation.md
│   │   ├── first-state-machine.md
│   │   └── core-concepts.md
│   ├── guides/
│   │   ├── index.md
│   │   ├── async-patterns.md
│   │   ├── analyzers.md
│   │   └── ...
│   ├── examples/
│   │   ├── index.md
│   │   └── ...
│   ├── architecture/
│   │   ├── index.md
│   │   ├── performance.md
│   │   └── ...
│   └── reference/
│       ├── cheat-sheet.md
│       └── ...
├── templates/              # Custom template
│   └── custom/
│       └── public/
│           └── main.css
└── images/                 # Images and diagrams
```

## Content Guidelines

### Writing Articles

- Use GitHub-flavored Markdown
- Include code examples with proper syntax highlighting
- Link to API reference using relative paths
- Add cross-references between related topics

### Code Examples

Use triple backticks with language identifier:

\`\`\`csharp
public class Example : StateMachineGrain<State, Trigger>
{
    // Your code here
}
\`\`\`

### Cross-References

Reference API members:
- Classes: `[StateMachineGrain](xref:Orleans.StateMachineES.StateMachineGrain-2)`
- Methods: `[FireAsync](xref:Orleans.StateMachineES.StateMachineGrain-2.FireAsync*)`

Reference articles:
- `[Getting Started](../getting-started/index.md)`
- `[Async Patterns](../guides/async-patterns.md)`

### Admonitions

Use blockquotes with emoji for notes:

```markdown
> **Note**: This is important information.

> **Warning**: This requires attention.

> **Tip**: Helpful advice.
```

## Customization

### Theme

The documentation uses the modern DocFx template with custom CSS in `templates/custom/public/main.css`.

To modify:
1. Edit `main.css` for styling changes
2. Update `docfx.json` globalMetadata for site-wide settings

### Logo

Place your logo at `images/logo.svg` and update `docfx.json`:

```json
"fileMetadata": {
  "_appLogoPath": {
    "**/*": "images/logo.svg"
  }
}
```

## GitHub Pages Deployment

Documentation is automatically built and deployed to GitHub Pages on every push to `main` branch via `.github/workflows/docs.yml`.

### Manual Deployment

```bash
# Build documentation
docfx docfx.json

# Deploy to gh-pages branch
cd _site
git init
git add .
git commit -m "Update documentation"
git push -f https://github.com/mivertowski/Orleans.StateMachineES.git main:gh-pages
```

## Troubleshooting

### Build Errors

**Issue**: "Project file not found"
```bash
# Ensure you're building from the solution root first
cd ..
dotnet build
cd docfx
docfx docfx.json
```

**Issue**: "Cannot find XML documentation"
```bash
# Enable XML documentation in project files
# Add to each .csproj:
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

### Missing Content

**Issue**: Articles not showing in navigation

Check:
1. Article is listed in appropriate `toc.yml`
2. Front matter is correct
3. File path matches `toc.yml` reference

**Issue**: API reference is empty

Check:
1. XML comments exist in source code
2. `GenerateDocumentationFile` is enabled
3. `filterConfig.yml` isn't excluding too much

## Contributing

When adding new documentation:

1. Create the article in the appropriate section
2. Add to the section's `toc.yml`
3. Cross-link from related articles
4. Include code examples
5. Build locally to verify
6. Submit pull request

## Resources

- [DocFx Documentation](https://dotnet.github.io/docfx/)
- [Markdown Guide](https://www.markdownguide.org/)
- [GitHub Pages](https://pages.github.com/)
