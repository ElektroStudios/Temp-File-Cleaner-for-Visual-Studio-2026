Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Collections.Generic

''' <summary>Recognizes narrowly scoped Visual Studio temporary names.</summary>
Friend NotInheritable Class CleanupRules

    Private Shared ReadOnly DirectoryNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "NuGetScratch",
        "Roslyn",
        "servicehub",
        "SymbolCache",
        "TFSTemp",
        "VBCSCompiler",
        "VisualStudioSourceGeneratedDocuments",
        "VS",
        "VSFeedbackIntelliCodeLogs",
        "VSGitHubCopilotLogs",
        "VSLogs",
        "VsTempFiles",
        "WPF"
    }

    ''' <summary>Checks a top-level temporary directory name.</summary>
    Public Shared Function IsDirectory(name As String) As Boolean
        If String.IsNullOrEmpty(name) Then Return False
        If DirectoryNames.Contains(name) Then Return True
        If name.StartsWith("pkg-", StringComparison.OrdinalIgnoreCase) Then
            Return name.Length = 10 AndAlso AllCharacters(name.Substring(4), "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
        End If
        If name.StartsWith("MSBuildTemp", StringComparison.OrdinalIgnoreCase) Then
            Return name.Length = 43 AndAlso AllCharacters(name.Substring(11), "0123456789abcdefABCDEF")
        End If
        If name.StartsWith("vs.mcj", StringComparison.OrdinalIgnoreCase) Then
            Return name.Length = 16 AndAlso AllCharacters(name.Substring(6), "0123456789")
        End If
        Dim value As Guid
        If name.StartsWith("Report.", StringComparison.OrdinalIgnoreCase) Then
            Return Guid.TryParseExact(name.Substring(7), "D", value)
        End If
        Return name.StartsWith("VS.", StringComparison.OrdinalIgnoreCase) AndAlso
            Guid.TryParseExact(name.Substring(3), "B", value)
    End Function

    ''' <summary>Checks top-level files; generic 8.3 names and VSIX packages are excluded.</summary>
    Public Shared Function IsFile(name As String) As Boolean
        If String.IsNullOrEmpty(name) Then Return False
        For Each prefix As String In {"AutoTempFileCleaner_VS2022_", "AutoTempFileCleaner_VS2026_"}
            If name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) AndAlso name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) Then
                Return name.Length = prefix.Length + 22 AndAlso AllCharacters(name.Substring(prefix.Length, 18), "0123456789")
            End If
        Next
        If name.StartsWith("dd_", StringComparison.OrdinalIgnoreCase) AndAlso name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) Then Return True
        If name.StartsWith("Report", StringComparison.OrdinalIgnoreCase) AndAlso name.EndsWith(".diagsession", StringComparison.OrdinalIgnoreCase) Then
            Return name.Length = 31 AndAlso name(14) = "-"c AndAlso
                AllCharacters(name.Substring(6, 8) & name.Substring(15, 4), "0123456789")
        End If
        If (name.StartsWith("dev", StringComparison.OrdinalIgnoreCase) OrElse name.StartsWith("TFR", StringComparison.OrdinalIgnoreCase)) AndAlso name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) Then
            Return name.Length >= 10 AndAlso AllCharacters(name.Substring(3, name.Length - 7), "0123456789abcdefABCDEF")
        End If
        Return name.StartsWith("startup", StringComparison.OrdinalIgnoreCase) AndAlso name.Length >= 17 AndAlso
            AllCharacters(name.Substring(7, 10), "0123456789")
    End Function

    ''' <summary>Requires a nonempty sequence of explicitly allowed characters.</summary>
    Private Shared Function AllCharacters(value As String, allowed As String) As Boolean
        If value.Length = 0 Then Return False
        For Each character As Char In value
            If allowed.IndexOf(character) < 0 Then Return False
        Next
        Return True
    End Function
End Class
