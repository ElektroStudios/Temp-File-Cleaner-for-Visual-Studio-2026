Option Strict On
Option Explicit On
Option Infer Off
Imports System
Imports System.IO

''' <summary>Owns an isolated directory; tests never operate on the real cleanup roots.</summary>
Friend NotInheritable Class TestDirectory
    Implements IDisposable
    Public ReadOnly Root As String = Path.Combine(Path.GetTempPath(), "CleanerTests-" & Guid.NewGuid().ToString("N"))
    Public Sub New()
        Directory.CreateDirectory(Me.Root)
    End Sub
    Public Function Write(relative As String, Optional content As String = "preserve me") As String
        Dim target As String = Path.Combine(Me.Root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(target))
        File.WriteAllText(target, content)
        Return target
    End Function
    Public Sub Dispose() Implements IDisposable.Dispose
        If Directory.Exists(Me.Root) Then Directory.Delete(Me.Root, True)
    End Sub
End Class
