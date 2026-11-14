Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>Serializes append operations and exposes write failures to the caller.</summary>
Public NotInheritable Class TextWriterService
    Implements ISTextWriterService, ITextWriterService

    ''' <summary>
    ''' Protects synchronous file writes performed exclusively on worker threads.
    ''' </summary>
    Private ReadOnly writeGate As New Object()

    ''' <summary>Initializes the service without accessing unnecessary shell services.</summary>
    Public Function InitializeAsync(cancellationToken As CancellationToken) As Task
        cancellationToken.ThrowIfCancellationRequested()
        Return Task.CompletedTask
    End Function

    ''' <summary>
    ''' Appends a UTF-8 line on a worker thread, serializing access to the file.
    ''' </summary>
    ''' <param name="path">The destination file path.</param>
    ''' <param name="line">The text to append, or Nothing for an empty line.</param>
    ''' <returns>A task representing the write operation.</returns>
    ''' <exception cref="ArgumentNullException">
    ''' <paramref name="path"/> is Nothing.
    ''' </exception>
    Public Async Function WriteLineAsync(path As String,
                                     line As String) As Task _
    Implements ITextWriterService.WriteLineAsync

        If path Is Nothing Then
            Throw New ArgumentNullException(NameOf(path))
        End If

        Await Task.Run(
        Sub()
            SyncLock Me.writeGate
                Using writer As New StreamWriter(
                    path,
                    append:=True,
                    encoding:=New UTF8Encoding(False))

                    writer.WriteLine(If(line, String.Empty))
                End Using
            End SyncLock
        End Sub).ConfigureAwait(False)
    End Function
End Class

''' <summary>Identifies the package's text writer service.</summary>
Public Interface ISTextWriterService
End Interface

''' <summary>Describes asynchronous line output.</summary>
Public Interface ITextWriterService
    ''' <summary>Appends a line, propagating filesystem failures.</summary>
    Function WriteLineAsync(path As String, line As String) As Task
End Interface
