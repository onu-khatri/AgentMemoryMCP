using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection;
using System.Xml.Linq;

namespace AgentSession.MCP.ArchitectureTests;

internal sealed record ProjectNode(string Path, string AssemblyName);
internal sealed record ProjectReferenceEdge(string Source, string Target);
internal sealed record PackageReferenceEdge(string Source, string Package);
internal sealed record AssemblyReferenceEdge(string Source, string Target);
internal sealed record AssemblyContractMetadata(
    string AssemblyName,
    IReadOnlySet<string> PublicTypeDefinitions,
    IReadOnlySet<string> TypeReferences,
    IReadOnlySet<string> ForwardedTypes);

internal sealed class RepositoryGraph
{
    private const TypeAttributes ForwarderAttribute = (TypeAttributes)0x00200000;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private RepositoryGraph(
        IReadOnlyList<string> solutionProjects,
        IReadOnlyList<string> missingProjectFiles,
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<ProjectReferenceEdge> projectReferences,
        IReadOnlyList<PackageReferenceEdge> packageReferences,
        IReadOnlyList<AssemblyReferenceEdge> assemblyReferences)
    {
        SolutionProjects = solutionProjects;
        MissingProjectFiles = missingProjectFiles;
        Projects = projects;
        ProjectReferences = projectReferences;
        PackageReferences = packageReferences;
        AssemblyReferences = assemblyReferences;
    }

    public IReadOnlyList<string> SolutionProjects { get; }
    public IReadOnlyList<string> MissingProjectFiles { get; }
    public IReadOnlyList<ProjectNode> Projects { get; }
    public IReadOnlyList<ProjectReferenceEdge> ProjectReferences { get; }
    public IReadOnlyList<PackageReferenceEdge> PackageReferences { get; }
    public IReadOnlyList<AssemblyReferenceEdge> AssemblyReferences { get; }

    public static RepositoryGraph Load(string root)
    {
        var normalizedRoot = NormalizeRoot(root);
        var solutionPath = Path.Combine(normalizedRoot, "AgentMemoryMCP.slnx");
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException("The repository graph requires AgentMemoryMCP.slnx.", solutionPath);

        var solution = XDocument.Load(solutionPath, LoadOptions.SetLineInfo);
        var projectPaths = solution
            .Descendants("Project")
            .Select(element => (string?)element.Attribute("Path"))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizeRelativePath(normalizedRoot, path!))
            .Distinct(PathComparer)
            .OrderBy(path => path, PathComparer)
            .ToArray();

        var assemblyPaths = new List<string>();
        var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Release";
        foreach (var relativeProjectPath in projectPaths.Where(IsProductionProject))
        {
            var projectPath = ResolveContainedPath(normalizedRoot, relativeProjectPath);
            if (!File.Exists(projectPath))
                continue;

            var project = XDocument.Load(projectPath);
            var assemblyName = project.Descendants("AssemblyName").Select(element => element.Value).FirstOrDefault()
                ?? Path.GetFileNameWithoutExtension(projectPath);
            assemblyPaths.Add(Path.Combine(
                Path.GetDirectoryName(projectPath)!,
                "bin",
                configuration,
                "net10.0",
                assemblyName + ".dll"));
        }

        return LoadCore(normalizedRoot, projectPaths, assemblyPaths, projectPaths);
    }

    public static RepositoryGraph LoadProjects(
        string root,
        IEnumerable<string> projectPaths,
        IEnumerable<string> assemblyPaths)
    {
        var normalizedRoot = NormalizeRoot(root);
        var normalizedProjects = NormalizeExplicitPaths(normalizedRoot, projectPaths, "project");
        var normalizedAssemblies = NormalizeExplicitPaths(normalizedRoot, assemblyPaths, "assembly")
            .Select(path => ResolveContainedPath(normalizedRoot, path))
            .ToArray();
        return LoadCore(normalizedRoot, normalizedProjects, normalizedAssemblies, Array.Empty<string>());
    }

    private static RepositoryGraph LoadCore(
        string root,
        IReadOnlyList<string> projectPaths,
        IReadOnlyList<string> assemblyPaths,
        IReadOnlyList<string> solutionProjects)
    {
        var missingProjects = projectPaths
            .Where(path => !File.Exists(ResolveContainedPath(root, path)))
            .OrderBy(path => path, PathComparer)
            .ToArray();
        var projects = new List<ProjectNode>();
        var projectReferences = new List<ProjectReferenceEdge>();
        var packageReferences = new List<PackageReferenceEdge>();

        foreach (var relativeProjectPath in projectPaths.Except(missingProjects, PathComparer))
        {
            var projectPath = ResolveContainedPath(root, relativeProjectPath);
            var document = XDocument.Load(projectPath, LoadOptions.SetLineInfo);
            var assemblyName = document.Descendants("AssemblyName").Select(element => element.Value).FirstOrDefault()
                ?? Path.GetFileNameWithoutExtension(projectPath);
            projects.Add(new ProjectNode(relativeProjectPath, assemblyName));

            foreach (var reference in document.Descendants("ProjectReference"))
            {
                var include = (string?)reference.Attribute("Include")
                    ?? throw new InvalidDataException($"ProjectReference in '{relativeProjectPath}' has no Include attribute.");
                var target = ResolveContainedPath(Path.GetDirectoryName(projectPath)!, include, root);
                projectReferences.Add(new ProjectReferenceEdge(relativeProjectPath, ToRelativePath(root, target)));
            }

            foreach (var package in document.Descendants("PackageReference"))
            {
                var include = (string?)package.Attribute("Include")
                    ?? throw new InvalidDataException($"PackageReference in '{relativeProjectPath}' has no Include attribute.");
                packageReferences.Add(new PackageReferenceEdge(relativeProjectPath, include));
            }
        }

        var assemblyReferences = new List<AssemblyReferenceEdge>();
        foreach (var assemblyPath in assemblyPaths)
        {
            var fullAssemblyPath = ResolveContainedPath(root, assemblyPath);
            if (!File.Exists(fullAssemblyPath))
                throw new FileNotFoundException(
                    $"Required compiled assembly is missing. Build Release before architecture tests: {ToRelativePath(root, fullAssemblyPath)}",
                    fullAssemblyPath);
            assemblyReferences.AddRange(ReadAssemblyReferences(root, fullAssemblyPath));
        }

        return new RepositoryGraph(
            solutionProjects.OrderBy(path => path, PathComparer).ToArray(),
            missingProjects,
            projects.OrderBy(project => project.Path, PathComparer).ToArray(),
            projectReferences.OrderBy(edge => edge.Source, PathComparer).ThenBy(edge => edge.Target, PathComparer).ToArray(),
            packageReferences.OrderBy(edge => edge.Source, PathComparer).ThenBy(edge => edge.Package, StringComparer.OrdinalIgnoreCase).ToArray(),
            assemblyReferences.OrderBy(edge => edge.Source, PathComparer).ThenBy(edge => edge.Target, StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<AssemblyReferenceEdge> ReadAssemblyReferences(string root, string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        if (!reader.HasMetadata)
            throw new InvalidDataException($"Assembly has no metadata: {ToRelativePath(root, assemblyPath)}");
        var metadata = reader.GetMetadataReader();
        var source = metadata.GetString(metadata.GetAssemblyDefinition().Name);
        foreach (var handle in metadata.AssemblyReferences)
            yield return new AssemblyReferenceEdge(source, metadata.GetString(metadata.GetAssemblyReference(handle).Name));
    }

    public static AssemblyContractMetadata ReadAssemblyContractMetadata(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        if (!reader.HasMetadata)
            throw new InvalidDataException($"Assembly has no metadata: {assemblyPath}");
        var metadata = reader.GetMetadataReader();
        var definitions = metadata.TypeDefinitions
            .Select(handle => metadata.GetTypeDefinition(handle))
            .Where(definition => definition.GetDeclaringType().IsNil &&
                (definition.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public)
            .Select(definition => FullName(metadata.GetString(definition.Namespace), metadata.GetString(definition.Name)))
            .ToHashSet(StringComparer.Ordinal);
        var references = metadata.TypeReferences
            .Select(handle => metadata.GetTypeReference(handle))
            .Select(reference => FullName(metadata.GetString(reference.Namespace), metadata.GetString(reference.Name)))
            .ToHashSet(StringComparer.Ordinal);
        var forwarders = metadata.ExportedTypes
            .Select(handle => metadata.GetExportedType(handle))
            .Where(exported => (exported.Attributes & ForwarderAttribute) != 0)
            .Select(exported => FullName(metadata.GetString(exported.Namespace), metadata.GetString(exported.Name)))
            .ToHashSet(StringComparer.Ordinal);
        return new(
            metadata.GetString(metadata.GetAssemblyDefinition().Name),
            definitions,
            references,
            forwarders);
    }

    private static string FullName(string typeNamespace, string name) =>
        string.IsNullOrEmpty(typeNamespace) ? name : typeNamespace + "." + name;

    private static string[] NormalizeExplicitPaths(string root, IEnumerable<string> paths, string label)
    {
        var normalized = paths.Select(path => NormalizeRelativePath(root, path)).ToArray();
        if (normalized.Distinct(PathComparer).Count() != normalized.Length)
            throw new InvalidDataException($"Duplicate {label} path supplied to repository graph loader.");
        return normalized;
    }

    private static string NormalizeRoot(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException(fullRoot);
        return fullRoot;
    }

    private static string NormalizeRelativePath(string root, string path) =>
        ToRelativePath(root, ResolveContainedPath(root, path));

    private static string ResolveContainedPath(string basePath, string path, string? containmentRoot = null)
    {
        var root = Path.GetFullPath(containmentRoot ?? basePath);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(basePath, path));
        var relative = Path.GetRelativePath(root, fullPath);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"Path escapes repository root: {path}");
        return fullPath;
    }

    private static string ToRelativePath(string root, string path) =>
        Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/');

    private static bool IsProductionProject(string path) => path.StartsWith("src/", StringComparison.OrdinalIgnoreCase);
}
