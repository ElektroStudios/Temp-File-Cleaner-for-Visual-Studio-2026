Option Strict On
Option Explicit On
Option Infer Off
Imports System
Imports System.Collections.Generic
Imports Xunit

''' <summary>Exercises positive and negative boundaries of every cleanup-name family.</summary>
Public Class CleanupRulesTests
    Public Shared Iterator Function Names() As IEnumerable(Of TheoryDataRow(Of String, Boolean, Boolean))
        For Each name As String In {"NuGetScratch", "VSFeedbackIntelliCodeLogs", "VS", "VSLogs", "VsTempFiles", "WPF", "TFSTemp", "servicehub", "SymbolCache", "VBCSCompiler", "VisualStudioSourceGeneratedDocuments", "VSGitHubCopilotLogs",
            "pkg-Ab012Z", "MSBuildTemp0123456789abcdef0123456789abcdef", "vs.mcj0123456789", "Report.01234567-89ab-cdef-0123-456789abcdef", "VS.{01234567-89ab-cdef-0123-456789abcdef}"}
            Yield New TheoryDataRow(Of String, Boolean, Boolean)(name, True, True)
            If name <> name.ToUpperInvariant() Then Yield New TheoryDataRow(Of String, Boolean, Boolean)(name.ToUpperInvariant(), True, True)
            Yield New TheoryDataRow(Of String, Boolean, Boolean)(name & "x", True, False)
            Yield New TheoryDataRow(Of String, Boolean, Boolean)("x" & name, True, False)
            Yield New TheoryDataRow(Of String, Boolean, Boolean)(name, False, False)
        Next
        For Each name As String In {"dd_install.log", "AutoTempFileCleaner_VS2022_123456789012345678.log", "AutoTempFileCleaner_VS2026_123456789012345678.log",
            "Report20261113-1234.diagsession", "devABC.tmp", "TFR012345.tmp", "startup0123456789", "startup0123456789.log"}
            Yield New TheoryDataRow(Of String, Boolean, Boolean)(name, False, True)
            Yield New TheoryDataRow(Of String, Boolean, Boolean)(name.ToUpperInvariant(), False, True)
            Yield New TheoryDataRow(Of String, Boolean, Boolean)("x" & name, False, False)
            Yield New TheoryDataRow(Of String, Boolean, Boolean)(name, True, False)
        Next
        For Each name As String In {Nothing, "", "document.txt", "accounts.csv", "12345678.abc", "VSIX12345678.vsix", "01234567-89ab-cdef-0123-456789abcdef",
            "pkg-ABC_DE", "pkg-ABC[DE", "pkg-ABC^DE", "pkg-ABCDE", "pkg-ABCDEFG", "pkg-áBCDEF", "MSBuildTempgggggggggggggggggggggggggggggggg",
            "Report.zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz", "VS.{zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz}", "vs.mcj123456789", "vs.mcj123456789a",
            "startup123456789", "startup123456789a.log", "devABnotesC.tmp", "TFRAB_notes_C.tmp", "devAB.tmp", "TFRxyz.tmp", "dd_install.txt",
            "Report2026111a-1234.diagsession", "Report20261113_1234.diagsession", "AutoTempFileCleaner_VS2026_12345678901234567.log", "AutoTempFileCleaner_VS2026_12345678901234567a.log"}
            Yield New TheoryDataRow(Of String, Boolean, Boolean)(name, False, False)
            Yield New TheoryDataRow(Of String, Boolean, Boolean)(name, True, False)
        Next
    End Function

    <Theory, MemberData(NameOf(Names))>
    Public Sub NamesMatchOnlyTheirIntendedCategory(name As String, directory As Boolean, expected As Boolean)
        Assert.Equal(expected, If(directory, CleanupRules.IsDirectory(name), CleanupRules.IsFile(name)))
    End Sub
End Class
