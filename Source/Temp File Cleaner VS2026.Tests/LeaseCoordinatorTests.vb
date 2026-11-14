Option Strict On
Option Explicit On
Option Infer Off
Imports System
Imports System.IO
Imports System.Threading
Imports Xunit

''' <summary>Verifies registry persistence, fail-closed behavior and serialized cleanup.</summary>
Public Class LeaseCoordinatorTests
    Private Shared Function Create(folder As TestDirectory, key As String, Optional probe As Func(Of Integer, Long, Boolean) = Nothing) As VsInstanceLeaseCoordinator
        Return New VsInstanceLeaseCoordinator(Path.Combine(folder.Root, "leases.txt"), "CleanerTest-" & Path.GetFileName(folder.Root), key,
            If(probe, Function(id As Integer, ticks As Long) id > 0 AndAlso ticks > 0), TimeSpan.FromSeconds(2))
    End Function

    <Fact>
    Public Sub UnregisteredInstanceCannotAuthorizeCleanup()
        Using folder As New TestDirectory(), coordinator As VsInstanceLeaseCoordinator = Create(folder, "1;1")
            Assert.False(coordinator.UnregisterLeaseAndRunIfLast(Sub() Assert.Fail("Unregistered cleanup executed.")))
            Assert.False(File.Exists(Path.Combine(folder.Root, "leases.txt")))
        End Using
    End Sub

    <Fact>
    Public Sub LastRegisteredInstanceCleansExactlyOnce()
        Using folder As New TestDirectory(), first As VsInstanceLeaseCoordinator = Create(folder, "1;1"), second As VsInstanceLeaseCoordinator = Create(folder, "2;2")
            Assert.True(first.RegisterLease())
            Assert.True(first.RegisterLease())
            Assert.False(second.RegisterLease())
            Assert.False(first.UnregisterLeaseAndRunIfLast(Sub() Assert.Fail("Another instance remains.")))
            Dim count As Integer = 0
            Assert.True(second.UnregisterLeaseAndRunIfLast(Sub() count += 1))
            Assert.False(second.UnregisterLeaseAndRunIfLast(Sub() count += 1))
            Assert.Equal(1, count)
            Assert.Empty(File.ReadAllLines(Path.Combine(folder.Root, "leases.txt")))
            Assert.Single(Directory.GetFiles(folder.Root))
        End Using
    End Sub

    <Fact>
    Public Sub StaleLeasesAreRemovedUsingBothIdentityFields()
        Using folder As New TestDirectory()
            folder.Write("leases.txt", "2;20" & Environment.NewLine & "3;30")
            Using coordinator As VsInstanceLeaseCoordinator = Create(folder, "1;10", Function(id As Integer, ticks As Long) id = 3 AndAlso ticks = 30)
                Assert.False(coordinator.RegisterLease())
                Assert.Equal(Of System.Collections.Generic.IEnumerable(Of String))({"1;10", "3;30"}, File.ReadAllLines(Path.Combine(folder.Root, "leases.txt")))
                Assert.False(coordinator.UnregisterLeaseAndRunIfLast(Sub() Assert.Fail("A live lease remains.")))
            End Using
        End Using
    End Sub

    <Theory>
    <InlineData("garbage")>
    <InlineData("1")>
    <InlineData("1;2;3")>
    <InlineData("0;1")>
    <InlineData("-1;1")>
    <InlineData("1;0")>
    <InlineData("1;-1")>
    <InlineData("1;9999999999999999999")>
    <InlineData("1;3155378976000000000")>
    <InlineData("2147483648;1")>
    <InlineData("1; 1")>
    <InlineData("a;1")>
    Public Sub CorruptRegistryDoesNotAuthorizeCleanup(contents As String)
        Using folder As New TestDirectory(), coordinator As VsInstanceLeaseCoordinator = Create(folder, "1;1")
            coordinator.RegisterLease()
            folder.Write("leases.txt", contents)
            Assert.Throws(Of InvalidDataException)(Sub() coordinator.UnregisterLeaseAndRunIfLast(Sub() Assert.Fail("Corrupt registry authorized cleanup.")))
            Assert.Equal(contents, File.ReadAllText(Path.Combine(folder.Root, "leases.txt")))
            ' Restore valid data so disposal can release the lease.
            folder.Write("leases.txt", "1;1")
        End Using
    End Sub

    <Fact>
    Public Sub DeletedRegistryDoesNotAuthorizeCleanup()
        Using folder As New TestDirectory(), coordinator As VsInstanceLeaseCoordinator = Create(folder, "1;1")
            coordinator.RegisterLease()
            File.Delete(Path.Combine(folder.Root, "leases.txt"))
            Assert.False(coordinator.UnregisterLeaseAndRunIfLast(Sub() Assert.Fail("Missing lease authorized cleanup.")))
        End Using
    End Sub

    <Fact>
    Public Sub ProbeFailurePropagatesWithoutReplacingRegistry()
        Using folder As New TestDirectory(), coordinator As VsInstanceLeaseCoordinator = Create(folder, "1;1", Function(id As Integer, ticks As Long) ThrowProbe(id, ticks))
            folder.Write("leases.txt", "2;2")
            Assert.Throws(Of UnauthorizedAccessException)(Sub() coordinator.RegisterLease())
            Assert.Equal("2;2", File.ReadAllText(Path.Combine(folder.Root, "leases.txt")))
            Assert.False(coordinator.UnregisterLeaseAndRunIfLast(Sub() Assert.Fail("Registration failed.")))
        End Using
    End Sub

    Private Shared Function ThrowProbe(id As Integer, ticks As Long) As Boolean
        Throw New UnauthorizedAccessException($"Cannot inspect {id};{ticks}.")
    End Function

    <Fact>
    Public Sub CleanupFailureIsNotRetriedAndReleasesMutex()
        Using folder As New TestDirectory(), first As VsInstanceLeaseCoordinator = Create(folder, "1;1"), second As VsInstanceLeaseCoordinator = Create(folder, "2;2")
            first.RegisterLease()
            Assert.Throws(Of IOException)(Sub() first.UnregisterLeaseAndRunIfLast(Sub() Throw New IOException("Expected failure.")))
            Assert.False(first.UnregisterLeaseAndRunIfLast(Sub() Assert.Fail("Cleanup retried.")))
            Assert.True(second.RegisterLease())
        End Using
    End Sub

    <Fact>
    Public Sub DisposedCoordinatorRejectsRegistration()
        Using folder As New TestDirectory()
            Dim coordinator As VsInstanceLeaseCoordinator = Create(folder, "1;1")
            coordinator.RegisterLease()
            coordinator.Dispose()
            coordinator.Dispose()
            Assert.Throws(Of ObjectDisposedException)(Sub() coordinator.RegisterLease())
            Assert.False(coordinator.UnregisterLeaseAndRunIfLast(Sub() Assert.Fail("Disposed cleanup executed.")))
        End Using
    End Sub

    <Fact>
    Public Sub RegistrationCannotEnterDuringCleanup()
        Using folder As New TestDirectory(), first As VsInstanceLeaseCoordinator = Create(folder, "1;1"), second As VsInstanceLeaseCoordinator = Create(folder, "2;2"), started As New ManualResetEventSlim(), finished As New ManualResetEventSlim()
            first.RegisterLease()
            Dim failure As Exception = Nothing
            Dim worker As New Thread(Sub()
                                         started.Set()
                                         Try
                                             second.RegisterLease()
                                         Catch ex As Exception
                                             failure = ex
                                         Finally
                                             finished.Set()
                                         End Try
                                     End Sub)
            Try
                Assert.True(first.UnregisterLeaseAndRunIfLast(
                    Sub()
                        worker.Start()
                        Assert.True(started.Wait(TimeSpan.FromSeconds(5)))
                        Assert.False(finished.Wait(TimeSpan.FromMilliseconds(150)))
                    End Sub))
                Assert.True(finished.Wait(TimeSpan.FromSeconds(5)))
                Assert.Null(failure)
                Assert.Equal(Of System.Collections.Generic.IEnumerable(Of String))({"2;2"}, File.ReadAllLines(Path.Combine(folder.Root, "leases.txt")))
            Finally
                If worker.IsAlive Then worker.Join(TimeSpan.FromSeconds(5))
            End Try
        End Using
    End Sub
    <Fact>
    Public Sub MutexTimeoutDoesNotWriteRegistryOrAuthorizeCleanup()
        Using folder As New TestDirectory(), ready As New ManualResetEventSlim(), release As New ManualResetEventSlim()
            Dim name As String = "CleanerTimeout-" & Guid.NewGuid().ToString("N")
            Dim holder As New Thread(Sub()
                                         Using gate As New Mutex(False, name)
                                             gate.WaitOne()
                                             ready.Set()
                                             release.Wait()
                                             gate.ReleaseMutex()
                                         End Using
                                     End Sub)
            holder.Start()
            Try
                Assert.True(ready.Wait(TimeSpan.FromSeconds(5)))
                Dim coordinator As New VsInstanceLeaseCoordinator(Path.Combine(folder.Root, "leases.txt"), name, "1;1",
                    Function(id As Integer, ticks As Long) id > 0 AndAlso ticks > 0, TimeSpan.FromMilliseconds(50))
                Assert.Throws(Of TimeoutException)(Sub() coordinator.RegisterLease())
                Assert.False(File.Exists(Path.Combine(folder.Root, "leases.txt")))
                release.Set()
                Assert.True(holder.Join(TimeSpan.FromSeconds(5)))
                Using coordinator
                    Assert.False(coordinator.UnregisterLeaseAndRunIfLast(Sub() Assert.Fail("Timed-out registration authorized cleanup.")))
                End Using
            Finally
                release.Set()
                holder.Join(TimeSpan.FromSeconds(5))
            End Try
        End Using
    End Sub

    <Theory>
    <InlineData(True)>
    <InlineData(False)>
    Public Sub ProcessProbeDistinguishesCurrentIdentityFromReusedPid(correctStartTime As Boolean)
        Using current As System.Diagnostics.Process = System.Diagnostics.Process.GetCurrentProcess()
            Dim ticks As Long = current.StartTime.ToUniversalTime().Ticks
            Assert.Equal(correctStartTime, VsInstanceLeaseCoordinator.IsProcessAlive(current.Id, If(correctStartTime, ticks, ticks - 1)))
        End Using
    End Sub

End Class
