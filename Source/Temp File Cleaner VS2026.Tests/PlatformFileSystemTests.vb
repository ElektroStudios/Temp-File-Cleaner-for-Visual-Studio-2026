Option Strict On
Option Explicit On
Option Infer Off
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Runtime.InteropServices
Imports Xunit

''' <summary>Checks link boundaries and sharing failures on the platforms that expose them.</summary>
Public Class PlatformFileSystemTests
#If Not WINDOWS_TESTS Then
    <DllImport("libc", EntryPoint:="symlink", SetLastError:=True)>
    Private Shared Function CreateUnixLink(target As String, link As String) As Integer
    End Function

    <Theory>
    <InlineData("VSLogs")>
    <InlineData("VSLogs/nested")>
    <InlineData("Roslyn")>
    <InlineData("Roslyn/AnalyzerAssemblyLoader")>
    Public Sub DoesNotFollowDirectoryLinks(relative As String)
        Using folder As New TestDirectory()
            Dim keep As String = folder.Write("outside/AnalyzerAssemblyLoader/important.txt")
            Dim link As String = Path.Combine(folder.Root, "temp", relative)
            Directory.CreateDirectory(Path.GetDirectoryName(link))
            Assert.Equal(0, CreateUnixLink(Path.Combine(folder.Root, "outside"), link))
            Try
                Dim reports As New List(Of String)()
                Dim runner As New CleanupRunner(Path.Combine(folder.Root, "temp"), Path.Combine(folder.Root, "local"), Path.Combine(folder.Root, "active.log"), AddressOf reports.Add)
                runner.Run()
                Assert.True(File.Exists(keep))
                Assert.Contains(reports, Function(line As String) line.StartsWith("SKIPPED REPARSE POINT:", StringComparison.Ordinal))
            Finally
                Directory.Delete(link)
            End Try
        End Using
    End Sub

    <Fact>
    Public Sub DoesNotFollowLinkedLocalDataRoot()
        Using folder As New TestDirectory()
            Dim keep As String = folder.Write("outside/Microsoft/VisualStudio/SettingsLogs/important.txt")
            Dim link As String = Path.Combine(folder.Root, "local")
            Assert.Equal(0, CreateUnixLink(Path.Combine(folder.Root, "outside"), link))
            Try
                Dim runner As New CleanupRunner(Path.Combine(folder.Root, "temp"), link, Path.Combine(folder.Root, "active.log"), Sub(message As String) GC.KeepAlive(message))
                runner.Run()
                Assert.True(File.Exists(keep))
            Finally
                Directory.Delete(link)
            End Try
        End Using
    End Sub

#Else
    <Fact>
    Public Sub LockedFileDoesNotPreventOtherCandidatesFromBeingDeleted()
        Using folder As New TestDirectory()
            Dim locked As String = folder.Write("temp/devABC.tmp")
            Dim removable As String = folder.Write("temp/TFR123.tmp")
            Dim reports As New List(Of String)()
            Using handle As New FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                Dim runner As New CleanupRunner(Path.Combine(folder.Root, "temp"), Path.Combine(folder.Root, "local"), Path.Combine(folder.Root, "active.log"), AddressOf reports.Add)
                runner.Run()
                Assert.True(File.Exists(locked))
                Assert.False(File.Exists(removable))
                Assert.Contains(reports, Function(line As String) line.StartsWith("ERROR:", StringComparison.Ordinal))
            End Using
        End Using
    End Sub
#End If
End Class
