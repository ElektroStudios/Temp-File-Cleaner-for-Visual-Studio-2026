Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading

''' <summary>Coordinates participating packages; unregistered Visual Studio processes are not tracked.</summary>
Friend NotInheritable Class VsInstanceLeaseCoordinator
    Implements IDisposable

    Private ReadOnly registryPath As String
    Private ReadOnly mutexName As String
    Private ReadOnly leaseKey As String
    Private ReadOnly isAlive As Func(Of Integer, Long, Boolean)
    Private ReadOnly timeout As TimeSpan
    Private registered As Boolean
    Private disposed As Boolean

    ''' <summary>Uses the current user's registry and current process identity.</summary>
    Public Sub New()
        Me.New(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "ElektroStudios", "AutoTempFileCleaner2026", "Visual Studio Process Instances.tmp"),
               "ElektroStudios.AutoTempFileCleaner.InstanceMutex", CurrentKey(), AddressOf IsProcessAlive, TimeSpan.FromSeconds(10))
    End Sub

    ''' <summary>Supplies isolated storage, process probing and lock timeout for deterministic tests.</summary>
    Friend Sub New(path As String, name As String, key As String, probe As Func(Of Integer, Long, Boolean), waitTimeout As TimeSpan)
        If String.IsNullOrWhiteSpace(name) Then Throw New ArgumentException("A mutex name is required.", NameOf(name))
        If probe Is Nothing Then Throw New ArgumentNullException(NameOf(probe))
        Dim processId As Integer
        Dim ticks As Long
        ParseKey(key, processId, ticks)
        Me.registryPath = System.IO.Path.GetFullPath(path)
        Me.mutexName = name
        Me.leaseKey = key
        Me.isAlive = probe
        Me.timeout = waitTimeout
    End Sub

    ''' <summary>Registers once and returns whether this is the only participating process.</summary>
    Public Function RegisterLease() As Boolean
        Using gate As New Mutex(False, Me.mutexName)
            Me.Acquire(gate)
            Try
                If Me.disposed Then Throw New ObjectDisposedException(NameOf(VsInstanceLeaseCoordinator))
                Dim leases As HashSet(Of String) = Me.LoadLeases()
                leases.Add(Me.leaseKey)
                Me.SaveLeases(leases)
                Me.registered = True
                Return leases.Count = 1
            Finally
                gate.ReleaseMutex()
            End Try
        End Using
    End Function

    ''' <summary>Runs cleanup once under the same mutex used by registration, only after a valid last lease.</summary>
    Public Function UnregisterLeaseAndRunIfLast(cleanup As Action) As Boolean
        If cleanup Is Nothing Then Throw New ArgumentNullException(NameOf(cleanup))
        Using gate As New Mutex(False, Me.mutexName)
            Me.Acquire(gate)
            Try
                If Not Me.registered OrElse Me.disposed Then Return False
                Dim leases As HashSet(Of String) = Me.LoadLeases()
                Dim hadLease As Boolean = leases.Remove(Me.leaseKey)
                Me.SaveLeases(leases)
                Me.registered = False
                If Not hadLease OrElse leases.Count <> 0 Then Return False
                cleanup()
                Return True
            Finally
                gate.ReleaseMutex()
            End Try
        End Using
    End Function

    ''' <summary>Releases a lease without performing destructive cleanup.</summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        If Me.disposed Then Return
        Try
            Me.UnregisterLeaseAndRunIfLast(Sub()
                                           End Sub)
        Finally
            Me.disposed = True
        End Try
    End Sub

    ''' <summary>Acquires the cross-process gate or fails without authorizing cleanup.</summary>
    Private Sub Acquire(gate As Mutex)
        Try
            If Not gate.WaitOne(Me.timeout) Then Throw New TimeoutException("The Visual Studio cleanup lease is busy.")
        Catch ex As AbandonedMutexException
            ' The mutex is owned now; registry validation still must succeed.
        End Try
    End Sub

    ''' <summary>Loads and validates the registry, retaining leases whose liveness is uncertain.</summary>
    Private Function LoadLeases() As HashSet(Of String)
        Dim leases As New HashSet(Of String)(StringComparer.Ordinal)
        If Not File.Exists(Me.registryPath) Then Return leases
        For Each line As String In File.ReadAllLines(Me.registryPath, Encoding.UTF8)
            If String.IsNullOrWhiteSpace(line) Then Continue For
            Dim processId As Integer
            Dim ticks As Long
            ParseKey(line, processId, ticks)
            If line = Me.leaseKey OrElse Me.isAlive(processId, ticks) Then leases.Add(line)
        Next
        Return leases
    End Function

    ''' <summary>Writes through a replacement file so interrupted writes do not truncate the live registry.</summary>
    Private Sub SaveLeases(leases As IEnumerable(Of String))
        Directory.CreateDirectory(Path.GetDirectoryName(Me.registryPath))
        Dim temporary As String = Me.registryPath & "." & Guid.NewGuid().ToString("N") & ".tmp"
        Try
            File.WriteAllLines(temporary, leases.OrderBy(Function(value As String) value, StringComparer.Ordinal), New UTF8Encoding(False))
            If File.Exists(Me.registryPath) Then
                File.Replace(temporary, Me.registryPath, Nothing)
            Else
                File.Move(temporary, Me.registryPath)
            End If
        Finally
            If File.Exists(temporary) Then File.Delete(temporary)
        End Try
    End Sub

    ''' <summary>Rejects malformed identities rather than treating unknown registry contents as dead processes.</summary>
    Private Shared Sub ParseKey(key As String, ByRef processId As Integer, ByRef ticks As Long)
        If key Is Nothing Then Throw New InvalidDataException("Missing lease identity.")
        Dim parts As String() = key.Split(";"c)
        If parts.Length <> 2 OrElse Not Integer.TryParse(parts(0), NumberStyles.None, CultureInfo.InvariantCulture, processId) OrElse
            Not Long.TryParse(parts(1), NumberStyles.None, CultureInfo.InvariantCulture, ticks) OrElse
            processId <= 0 OrElse ticks <= 0 OrElse ticks > Date.MaxValue.Ticks Then
            Throw New InvalidDataException("Invalid lease identity: " & key)
        End If
    End Sub

    ''' <summary>Builds a culture-independent process identity and releases its process handle.</summary>
    Private Shared Function CurrentKey() As String
        Using current As Process = Process.GetCurrentProcess()
            Return current.Id.ToString(CultureInfo.InvariantCulture) & ";" &
                current.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)
        End Using
    End Function

    ''' <summary>Only treats a missing process or confirmed PID reuse as an expired lease.</summary>
    Friend Shared Function IsProcessAlive(processId As Integer, ticks As Long) As Boolean
        Try
            Using instance As Process = Process.GetProcessById(processId)
                Return instance.StartTime.ToUniversalTime().Ticks = ticks
            End Using
        Catch ex As ArgumentException
            Return False
        Catch ex As InvalidOperationException
            Return False
        Catch ex As Win32Exception
            Return True
        Catch ex As UnauthorizedAccessException
            Return True
        End Try
    End Function
End Class
