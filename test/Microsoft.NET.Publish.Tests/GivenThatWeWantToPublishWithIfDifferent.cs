// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishWithIfDifferent : SdkTest
    {
        public GivenThatWeWantToPublishWithIfDifferent(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_publishes_content_files_with_IfDifferent_metadata()
        {
            var testProject = new TestProject()
            {
                Name = "PublishWithIfDifferent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = @"using System;
class Program { static void Main() => Console.WriteLine(""Hello""); }";

            testProject.SourceFiles["data1.txt"] = "Data file 1 content";
            testProject.SourceFiles["data2.txt"] = "Data file 2 content";
            testProject.SourceFiles["data3.txt"] = "Data file 3 content";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""data1.txt"" CopyToPublishDirectory=""IfDifferent"" />
    <Content Include=""data2.txt"" CopyToPublishDirectory=""Always"" />
    <Content Include=""data3.txt"" CopyToPublishDirectory=""PreserveNewest"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            publishDirectory.Should().HaveFile("data1.txt");
            publishDirectory.Should().HaveFile("data2.txt");
            publishDirectory.Should().HaveFile("data3.txt");

            File.ReadAllText(Path.Combine(publishDirectory.FullName, "data1.txt")).Should().Be("Data file 1 content");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "data2.txt")).Should().Be("Data file 2 content");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "data3.txt")).Should().Be("Data file 3 content");
        }

        [Fact]
        public void It_skips_unchanged_files_with_IfDifferent_on_republish()
        {
            var testProject = new TestProject()
            {
                Name = "PublishIfDifferentSkipUnchanged",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = @"using System;
class Program { static void Main() => Console.WriteLine(""Hello""); }";

            testProject.SourceFiles["unchangedData.txt"] = "Original content";
            testProject.SourceFiles["changedData.txt"] = "Original content";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""unchangedData.txt"" CopyToPublishDirectory=""IfDifferent"" />
    <Content Include=""changedData.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            // First publish
            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            var unchangedFileInfo = new FileInfo(Path.Combine(publishDirectory.FullName, "unchangedData.txt"));
            var changedFileInfo = new FileInfo(Path.Combine(publishDirectory.FullName, "changedData.txt"));

            unchangedFileInfo.Exists.Should().BeTrue();
            changedFileInfo.Exists.Should().BeTrue();

            var unchangedOriginalTime = unchangedFileInfo.LastWriteTimeUtc;
            var changedOriginalTime = changedFileInfo.LastWriteTimeUtc;

            System.Threading.Thread.Sleep(1000);

            // Modify only one source file
            var changedSourcePath = Path.Combine(testAsset.Path, testProject.Name, "changedData.txt");
            File.WriteAllText(changedSourcePath, "Modified content");

            // Second publish
            publishCommand.Execute().Should().Pass();

            unchangedFileInfo.Refresh();
            changedFileInfo.Refresh();

            unchangedFileInfo.LastWriteTimeUtc.Should().Be(unchangedOriginalTime);
            changedFileInfo.LastWriteTimeUtc.Should().BeAfter(changedOriginalTime);

            File.ReadAllText(Path.Combine(publishDirectory.FullName, "unchangedData.txt")).Should().Be("Original content");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "changedData.txt")).Should().Be("Modified content");
        }

        [Fact]
        public void It_handles_None_items_with_IfDifferent_metadata()
        {
            var testProject = new TestProject()
            {
                Name = "PublishNoneWithIfDifferent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["config.json"] = "{ \"setting\": \"value\" }";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <None Include=""config.json"" CopyToPublishDirectory=""IfDifferent"">
      <CopyToOutputDirectory>Never</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            publishDirectory.Should().HaveFile("config.json");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "config.json")).Should().Be("{ \"setting\": \"value\" }");
        }

        [Fact]
        public void It_handles_Compile_items_with_IfDifferent_metadata()
        {
            var testProject = new TestProject()
            {
                Name = "PublishCompileWithIfDifferent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["SourceFile.cs"] = @"
namespace PublishCompileWithIfDifferent
{
    public class SourceClass { }
}";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Compile Update=""SourceFile.cs"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            publishDirectory.Should().HaveFile("SourceFile.cs");
        }

        [Fact]
        public void It_copies_IfDifferent_files_correctly_with_referenced_projects()
        {
            var referencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            referencedProject.SourceFiles["shared.txt"] = "Shared content from library";

            var mainProject = new TestProject()
            {
                Name = "MainProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                ReferencedProjects = { referencedProject }
            };

            mainProject.SourceFiles["Program.cs"] = @"using System;
class Program { static void Main() => Console.WriteLine(""Hello""); }";

            mainProject.SourceFiles["main.txt"] = "Main project content";

            var testAsset = TestAssetsManager.CreateTestProject(mainProject);

            var referencedProjectFile = Path.Combine(testAsset.Path, referencedProject.Name, $"{referencedProject.Name}.csproj");
            var referencedProjectContent = File.ReadAllText(referencedProjectFile);
            referencedProjectContent = referencedProjectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""shared.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(referencedProjectFile, referencedProjectContent);

            var mainProjectFile = Path.Combine(testAsset.Path, mainProject.Name, $"{mainProject.Name}.csproj");
            var mainProjectContent = File.ReadAllText(mainProjectFile);
            mainProjectContent = mainProjectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""main.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(mainProjectFile, mainProjectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(mainProject.TargetFrameworks);

            publishDirectory.Should().HaveFile("main.txt");
            publishDirectory.Should().HaveFile("shared.txt");

            File.ReadAllText(Path.Combine(publishDirectory.FullName, "main.txt")).Should().Be("Main project content");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "shared.txt")).Should().Be("Shared content from library");
        }

        [Fact]
        public void It_handles_mixed_CopyToPublishDirectory_metadata_values()
        {
            var testProject = new TestProject()
            {
                Name = "MixedCopyMetadata",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["always.txt"] = "Always copy";
            testProject.SourceFiles["preserveNewest.txt"] = "PreserveNewest copy";
            testProject.SourceFiles["ifDifferent.txt"] = "IfDifferent copy";
            testProject.SourceFiles["doNotCopy.txt"] = "Do not copy";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""always.txt"" CopyToPublishDirectory=""Always"" />
    <Content Include=""preserveNewest.txt"" CopyToPublishDirectory=""PreserveNewest"" />
    <Content Include=""ifDifferent.txt"" CopyToPublishDirectory=""IfDifferent"" />
    <Content Include=""doNotCopy.txt"" CopyToPublishDirectory=""Never"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            publishDirectory.Should().HaveFile("always.txt");
            publishDirectory.Should().HaveFile("preserveNewest.txt");
            publishDirectory.Should().HaveFile("ifDifferent.txt");
            publishDirectory.Should().NotHaveFile("doNotCopy.txt");
        }

        [Fact]
        public void It_publishes_IfDifferent_files_with_TargetPath()
        {
            var testProject = new TestProject()
            {
                Name = "IfDifferentWithTargetPath",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles[Path.Combine("source", "data.txt")] = "Data in subfolder";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", $@"
  <ItemGroup>
    <Content Include=""{Path.Combine("source", "data.txt")}"" CopyToPublishDirectory=""IfDifferent"">
      <TargetPath>{Path.Combine("output", "data.txt")}</TargetPath>
    </Content>
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            var targetFile = Path.Combine(publishDirectory.FullName, "output", "data.txt");
            File.Exists(targetFile).Should().BeTrue();
            File.ReadAllText(targetFile).Should().Be("Data in subfolder");
        }

        [Fact]
        public void It_handles_IfDifferent_with_self_contained_publish()
        {
            var testProject = new TestProject()
            {
                Name = "IfDifferentSelfContained",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework)
            };

            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["appdata.txt"] = "Application data";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""appdata.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier);

            publishDirectory.Should().HaveFile("appdata.txt");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "appdata.txt")).Should().Be("Application data");
        }

        [Fact]
        public void It_publishes_content_from_imported_targets_with_correct_path()
        {
            var testProject = new TestProject()
            {
                Name = "PublishImportedContent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectDirectory = Path.Combine(testAsset.Path, testProject.Name);

            var externalImportsDir = Path.Combine(testAsset.Path, "ExternalImports");
            Directory.CreateDirectory(externalImportsDir);

            var contentFile = Path.Combine(projectDirectory, "project-content.txt");
            File.WriteAllText(contentFile, "Content defined by external targets");

            var importedTargetsFile = Path.Combine(externalImportsDir, "ImportedContent.targets");
            File.WriteAllText(importedTargetsFile, $@"<Project>
  <ItemGroup>
    <Content Include=""$(MSBuildProjectDirectory)\project-content.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");

            var projectFile = Path.Combine(projectDirectory, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <Import Project=""..\ExternalImports\ImportedContent.targets"" />
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            publishDirectory.Should().HaveFile("project-content.txt");

            var publishedContentPath = Path.Combine(publishDirectory.FullName, "project-content.txt");
            File.ReadAllText(publishedContentPath).Should().Be("Content defined by external targets");

            var parentDir = Directory.GetParent(publishDirectory.FullName);
            var potentialEscapedFiles = Directory.GetFiles(parentDir.FullName, "project-content.txt", SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(publishDirectory.FullName));
            potentialEscapedFiles.Should().BeEmpty("Content file should not escape to directories outside publish folder");
        }

        [Fact]
        public void It_propagates_CopyToOutputDirectory_IfDifferent_to_CopyToPublishDirectory()
        {
            // Verifies the DefaultCopyToPublishDirectoryMetadata target propagates
            // CopyToOutputDirectory=IfDifferent to CopyToPublishDirectory when not explicitly set.
            var testProject = new TestProject()
            {
                Name = "PropagateIfDifferent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["propagated.txt"] = "Propagated content";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            // Only set CopyToOutputDirectory, not CopyToPublishDirectory
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""propagated.txt"" CopyToOutputDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // The file should be published even though only CopyToOutputDirectory was set
            publishDirectory.Should().HaveFile("propagated.txt");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "propagated.txt")).Should().Be("Propagated content");
        }

        [Fact]
        public void It_resolves_IfDifferent_content_items_into_ResolvedFileToPublish()
        {
            // Verifies that Content items with CopyToPublishDirectory=IfDifferent are
            // correctly included in ResolvedFileToPublish with the right metadata.
            var testProject = new TestProject()
            {
                Name = "IfDifferentResolved",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["resolved.txt"] = "Resolved content";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""resolved.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var getValuesCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                testProject.TargetFrameworks,
                "ResolvedFileToPublish",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "ComputeFilesToPublish",
                MetadataNames = { "CopyToPublishDirectory", "RelativePath" },
            };

            getValuesCommand.Execute().Should().Pass();

            var items = getValuesCommand.GetValuesWithMetadata();
            var ifDifferentItems = items.Where(i =>
                i.metadata["CopyToPublishDirectory"] == "IfDifferent" &&
                i.value.EndsWith("resolved.txt"));

            ifDifferentItems.Should().ContainSingle("There should be exactly one IfDifferent item for resolved.txt");
            ifDifferentItems.First().metadata["RelativePath"].Should().Be("resolved.txt");
        }

        [Fact]
        public void It_publishes_content_with_only_CopyToPublishDirectory_set()
        {
            // Verifies the _IncludeContentItemsWithOnlyPublishMetadata target correctly
            // includes Content items that have CopyToPublishDirectory set but no CopyToOutputDirectory.
            var testProject = new TestProject()
            {
                Name = "PublishOnlyContent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["publish-only.txt"] = "Publish only content";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            // Set CopyToPublishDirectory but NOT CopyToOutputDirectory
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""publish-only.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            publishDirectory.Should().HaveFile("publish-only.txt");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "publish-only.txt")).Should().Be("Publish only content");

            // Also verify it's not in the build output (since CopyToOutputDirectory was not set)
            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var buildDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            buildDirectory.Should().NotHaveFile("publish-only.txt");
        }

        [Fact]
        public void It_handles_EmbeddedResource_items_with_IfDifferent_metadata()
        {
            var testProject = new TestProject()
            {
                Name = "PublishEmbeddedResourceIfDiff",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectDir = Path.Combine(testAsset.Path, testProject.Name);
            File.WriteAllText(Path.Combine(projectDir, "resource.resx"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <data name=""TestString"" xml:space=""preserve"">
    <value>Test value</value>
  </data>
</root>");

            var projectFile = Path.Combine(projectDir, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <EmbeddedResource Include=""resource.resx"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            publishDirectory.Should().HaveFile("resource.resx");
        }

        [Fact]
        public void It_includes_IfDifferent_items_in_AllPublishItemsFullPathWithTargetPath()
        {
            // Verifies that IfDifferent items are included in AllPublishItemsFullPathWithTargetPath,
            // which is the combined list used by various downstream targets.
            var testProject = new TestProject()
            {
                Name = "IfDiffAllPublishItems",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["item.txt"] = "All publish items content";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""item.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var getValuesCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                testProject.TargetFrameworks,
                "AllPublishItemsFullPathWithTargetPath",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "GetCopyToPublishDirectoryItems",
                MetadataNames = { "TargetPath" },
            };

            getValuesCommand.Execute().Should().Pass();

            var items = getValuesCommand.GetValuesWithMetadata();
            var itemTxtEntries = items.Where(i => i.value.EndsWith("item.txt"));
            itemTxtEntries.Should().ContainSingle("IfDifferent item should be in AllPublishItemsFullPathWithTargetPath");
        }

        [Fact]
        public void It_propagates_IfDifferent_for_None_items_via_CopyToOutputDirectory()
        {
            // Verifies DefaultCopyToPublishDirectoryMetadata target propagates IfDifferent
            // from CopyToOutputDirectory to CopyToPublishDirectory for None items.
            var testProject = new TestProject()
            {
                Name = "PropagateNoneIfDifferent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["settings.json"] = "{ \"key\": \"value\" }";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            // Set CopyToOutputDirectory=IfDifferent on a None item, don't set CopyToPublishDirectory
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <None Include=""settings.json"" CopyToOutputDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // The None item should be published because CopyToOutputDirectory=IfDifferent propagates
            publishDirectory.Should().HaveFile("settings.json");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "settings.json")).Should().Be("{ \"key\": \"value\" }");
        }

        [Fact]
        public void It_propagates_IfDifferent_for_Compile_items_via_CopyToOutputDirectory()
        {
            // Verifies DefaultCopyToPublishDirectoryMetadata target propagates IfDifferent
            // from CopyToOutputDirectory to CopyToPublishDirectory for Compile items.
            var testProject = new TestProject()
            {
                Name = "PropagateCompileIfDiff",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["Extra.cs"] = "public class Extra { }";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            // Set CopyToOutputDirectory=IfDifferent on a Compile item, don't set CopyToPublishDirectory
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Compile Update=""Extra.cs"" CopyToOutputDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // The Compile item should be published because CopyToOutputDirectory=IfDifferent propagates
            publishDirectory.Should().HaveFile("Extra.cs");
        }

        [Fact]
        public void It_does_not_publish_IfDifferent_content_with_CopyToPublishDirectory_Never()
        {
            // Verifies that CopyToPublishDirectory=Never overrides CopyToOutputDirectory=IfDifferent
            var testProject = new TestProject()
            {
                Name = "NeverOverridesIfDiff",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["build-only.txt"] = "Build only content";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""build-only.txt"" CopyToOutputDirectory=""IfDifferent"" CopyToPublishDirectory=""Never"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // CopyToPublishDirectory=Never should prevent the file from being published
            publishDirectory.Should().NotHaveFile("build-only.txt");
        }

        [Fact]
        public void It_publishes_content_with_Link_metadata_and_IfDifferent()
        {
            // Verifies the _IncludeContentItemsWithOnlyPublishMetadata target
            // respects the Link metadata for computing TargetPath.
            var testProject = new TestProject()
            {
                Name = "IfDifferentWithLink",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectDir = Path.Combine(testAsset.Path, testProject.Name);

            // Create a file outside the project directory
            var externalDir = Path.Combine(testAsset.Path, "external");
            Directory.CreateDirectory(externalDir);
            File.WriteAllText(Path.Combine(externalDir, "external-data.txt"), "External data content");

            var projectFile = Path.Combine(projectDir, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""..\external\external-data.txt"" CopyToPublishDirectory=""IfDifferent"">
      <Link>linked-data.txt</Link>
    </Content>
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // The file should be published with the Link name, not the original name
            publishDirectory.Should().HaveFile("linked-data.txt");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "linked-data.txt")).Should().Be("External data content");
        }
    }
}
