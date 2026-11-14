Option Strict On
Option Explicit On
Option Infer Off
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Xunit

''' <summary>Verifies actual filesystem effects inside owned temporary roots.</summary>
Public Class CleanupRunnerTests
    <Theory>
    <InlineData("NuGetScratch")>
    <InlineData("VSLogs")>
    <InlineData("WPF")>
    <InlineData("pkg-Ab012Z")>
    <InlineData("Roslyn/AnalyzerAssemblyLoader")>
    <InlineData("VisualStudio/copilot-vs")>
    Public Sub RemovesRecognizedTreesOnly(relative As String)
        Using folder As New TestDirectory()
            Dim victim As String = folder.Write(Path.Combine("temp", relative, "nested", "cache.dat"))
            Dim keep As String = folder.Write("temp/unrelated/document.txt")
            Dim entries As New List(Of String)()
            Dim runner As New CleanupRunner(Path.Combine(folder.Root, "temp"), Path.Combine(folder.Root, "local"), Path.Combine(folder.Root, "active.log"), AddressOf entries.Add)
            runner.Run()
            runner.Run()
            Assert.False(File.Exists(victim))
            Assert.True(File.Exists(keep))
            Assert.True(Directory.Exists(Path.Combine(folder.Root, "temp")))
            Assert.Contains(entries, Function(line As String) line.StartsWith("SUCCESS: ", StringComparison.Ordinal))
        End Using
    End Sub

    <Theory>
    <InlineData("document.txt")>
    <InlineData("accounts.csv")>
    <InlineData("VSIX12345678.vsix")>
    <InlineData("devABnotesC.tmp")>
    <InlineData("Microsoft.CodeAnalysis.dll")>
    Public Sub PreservesUnrelatedTopLevelFiles(name As String)
        Using folder As New TestDirectory()
            Dim keep As String = folder.Write(Path.Combine("temp", name))
            Dim runner As New CleanupRunner(Path.Combine(folder.Root, "temp"), Path.Combine(folder.Root, "local"), Path.Combine(folder.Root, "active.log"), Sub(message As String) Assert.DoesNotContain("SUCCESS", message))
            runner.Run()
            Assert.Equal("preserve me", File.ReadAllText(keep))
        End Using
    End Sub

    <Fact>
    Public Sub PreservesActiveLogAndUnrelatedNestedFiles()
        Using folder As New TestDirectory()
            Dim active As String = folder.Write("temp/AutoTempFileCleaner_VS2026_123456789012345678.log")
            Dim old As String = folder.Write("temp/AutoTempFileCleaner_VS2022_123456789012345678.log")
            Dim nested As String = folder.Write("temp/unrelated/startup0123456789.log")
            Dim dll As String = folder.Write("temp/unrelated/Microsoft.CodeAnalysis.dll")
            Dim runner As New CleanupRunner(Path.Combine(folder.Root, "temp"), Path.Combine(folder.Root, "local"), active, Sub(message As String) GC.KeepAlive(message))
            runner.Run()
            Assert.True(File.Exists(active))
            Assert.False(File.Exists(old))
            Assert.True(File.Exists(nested))
            Assert.True(File.Exists(dll))
        End Using
    End Sub

    <Fact>
    Public Sub RemovesSettingsLogsWithoutRemovingAncestorDirectories()
        Using folder As New TestDirectory()
            Dim victim As String = folder.Write("local/Microsoft/VisualStudio/SettingsLogs/nested/log.txt")
            Dim keep As String = folder.Write("local/Microsoft/VisualStudio/settings.json")
            Dim runner As New CleanupRunner(Path.Combine(folder.Root, "missing"), Path.Combine(folder.Root, "local"), Path.Combine(folder.Root, "active.log"), Sub(message As String) GC.KeepAlive(message))
            runner.Run()
            Assert.False(File.Exists(victim))
            Assert.True(File.Exists(keep))
            Assert.True(Directory.Exists(Path.Combine(folder.Root, "local")))
        End Using
    End Sub

    <Fact>
    Public Sub RemovingLastFileDoesNotRemoveTempRoot()
        Using folder As New TestDirectory()
            folder.Write("temp/devABC.tmp")
            Dim root As String = Path.Combine(folder.Root, "temp")
            Dim runner As New CleanupRunner(root, Path.Combine(folder.Root, "local"), Path.Combine(folder.Root, "active.log"), Sub(message As String) GC.KeepAlive(message))
            runner.Run()
            Assert.True(Directory.Exists(root))
            Assert.Empty(Directory.GetFileSystemEntries(root))
        End Using
    End Sub
End Class
