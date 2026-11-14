Option Strict On
Option Explicit On
Option Infer Off
Imports System
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports Xunit

''' <summary>Checks real output, errors, cancellation and concurrent append behavior.</summary>
Public Class TextWriterServiceTests
    <Fact>
    Public Async Function MissingDirectoryFailureIsObservableAndDoesNotPoisonNextWrite() As Task
        Using folder As New TestDirectory()
            Dim service As New TextWriterService()
            Await Assert.ThrowsAsync(Of DirectoryNotFoundException)(Function() service.WriteLineAsync(Path.Combine(folder.Root, "missing/log.txt"), "test")).ConfigureAwait(False)
            Dim target As String = Path.Combine(folder.Root, "log.txt")
            Await service.WriteLineAsync(target, "recovered").ConfigureAwait(False)
            Assert.Equal("recovered" & Environment.NewLine, File.ReadAllText(target))
        End Using
    End Function

    <Fact>
    Public Async Function ConcurrentWritesProduceEveryCompleteLineOnce() As Task
        Using folder As New TestDirectory()
            Dim target As String = Path.Combine(folder.Root, "log.txt")
            Dim service As New TextWriterService()
            Dim writes As Task() = Enumerable.Range(0, 100).Select(Function(index As Integer) service.WriteLineAsync(target, "line " & index.ToString(Globalization.CultureInfo.InvariantCulture))).ToArray()
            Await Task.WhenAll(writes).ConfigureAwait(False)
            Dim lines As String() = File.ReadAllLines(target)
            Assert.Equal(100, lines.Length)
            Assert.Equal(100, lines.Distinct().Count())
            For index As Integer = 0 To 99
                Assert.Contains("line " & index.ToString(Globalization.CultureInfo.InvariantCulture), lines)
            Next
        End Using
    End Function

    <Theory>
    <InlineData("ñ 日本語 😀")>
    <InlineData("")>
    <InlineData(Nothing)>
    Public Async Function WritesUtf8WithoutBom(line As String) As Task
        Using folder As New TestDirectory()
            Dim target As String = Path.Combine(folder.Root, "log.txt")
            Await New TextWriterService().WriteLineAsync(target, line).ConfigureAwait(False)
            Assert.Equal(Of System.Collections.Generic.IEnumerable(Of Byte))(New UTF8Encoding(False).GetBytes(line & Environment.NewLine), File.ReadAllBytes(target))
        End Using
    End Function

    <Fact>
    Public Sub InitializationHonorsCancellation()
        Dim service As New TextWriterService()
        Assert.Throws(Of OperationCanceledException)(Sub() service.InitializeAsync(New CancellationToken(True)))
        Assert.True(service.InitializeAsync(CancellationToken.None).IsCompleted)
    End Sub

    <Fact>
    Public Async Function NullPathIsRejected() As Task
        Await Assert.ThrowsAsync(Of ArgumentNullException)(Function() New TextWriterService().WriteLineAsync(Nothing, "test")).ConfigureAwait(False)
    End Function
End Class
