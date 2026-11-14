Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.IO

''' <summary>Deletes recognized entries without following reparse points or deleting storage roots.</summary>
Friend NotInheritable Class CleanupRunner
    Private ReadOnly tempRoot As String
    Private ReadOnly localRoot As String
    Private ReadOnly activeLog As String
    Private ReadOnly report As Action(Of String)

    ''' <summary>Creates a runner for explicit roots and an error-reporting callback.</summary>
    Public Sub New(tempDirectory As String, localDirectory As String, logPath As String, reporter As Action(Of String))
        If reporter Is Nothing Then Throw New ArgumentNullException(NameOf(reporter))
        Me.tempRoot = Path.GetFullPath(tempDirectory)
        Me.localRoot = Path.GetFullPath(localDirectory)
        Me.activeLog = Path.GetFullPath(logPath)
        Me.report = reporter
    End Sub

    ''' <summary>Runs once, containing expected filesystem failures to individual entries.</summary>
    Public Sub Run()
        Me.VisitRoot(Me.tempRoot)
        Me.DeleteRelative(Me.tempRoot, Path.Combine("VisualStudio", "copilot-vs"))
        Me.DeleteRelative(Me.localRoot, Path.Combine("Microsoft", "VisualStudio", "SettingsLogs"))
    End Sub

    ''' <summary>Enumerates top-level candidates; unrelated directories are never searched.</summary>
    Private Sub VisitRoot(root As String)
        Try
            If Not IsSafePath(root) OrElse Not Directory.Exists(root) Then Return
            For Each item As FileSystemInfo In New DirectoryInfo(root).GetFileSystemInfos()
                If TypeOf item Is DirectoryInfo Then
                    If CleanupRules.IsDirectory(item.Name) Then Me.DeleteEntry(item.FullName)
                ElseIf CleanupRules.IsFile(item.Name) Then
                    Me.DeleteEntry(item.FullName)
                End If
            Next
        Catch ex As IOException
            Me.report("ERROR: " & root & " | " & ex.Message)
        Catch ex As UnauthorizedAccessException
            Me.report("ACCESS DENIED: " & root & " | " & ex.Message)
        End Try
    End Sub

    ''' <summary>Checks every ancestor before visiting an explicitly recognized relative directory.</summary>
    Private Sub DeleteRelative(root As String, relativePath As String)
        Me.DeleteEntry(Path.Combine(root, relativePath))
    End Sub

    ''' <summary>Deletes children individually; failed or linked children keep their parent intact.</summary>
    Private Sub DeleteEntry(path As String)
        Try
            If String.Equals(path, Me.activeLog, StringComparison.OrdinalIgnoreCase) Then Return
            If Not IsSafePath(path) Then
                Me.report("SKIPPED REPARSE POINT: " & path)
                Return
            End If
            If Directory.Exists(path) Then
                For Each child As FileSystemInfo In New DirectoryInfo(path).GetFileSystemInfos()
                    Me.DeleteEntry(child.FullName)
                Next
                Directory.Delete(path, recursive:=False)
            ElseIf File.Exists(path) Then
                File.Delete(path)
            Else
                Return
            End If
            Me.report("SUCCESS: " & path)
        Catch ex As IOException
            Me.report("ERROR: " & path & " | " & ex.Message)
        Catch ex As UnauthorizedAccessException
            Me.report("ACCESS DENIED: " & path & " | " & ex.Message)
        End Try
    End Sub

    ''' <summary>Rejects existing reparse points anywhere along the absolute path.</summary>
    Private Shared Function IsSafePath(path As String) As Boolean
        Dim current As String = System.IO.Path.GetFullPath(path)
        Do While Not String.IsNullOrEmpty(current)
            If (Directory.Exists(current) OrElse File.Exists(current)) AndAlso
                (File.GetAttributes(current) And FileAttributes.ReparsePoint) <> 0 Then Return False
            current = System.IO.Path.GetDirectoryName(current)
        Loop
        Return True
    End Function
End Class
