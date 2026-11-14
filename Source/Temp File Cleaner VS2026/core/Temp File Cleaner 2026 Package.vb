Option Strict On
Option Explicit On
Option Infer Off

Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Shell
Imports Microsoft.VisualStudio.Shell.Interop
Imports Temp_File_Cleaner_VS2026.MyPackage

''' <summary>Registers cleanup services and cleans recognized temporary entries during package disposal.</summary>
<ProvideService(GetType(ISTextWriterService), IsAsyncQueryable:=True)>
<ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)>
<PackageRegistration(UseManagedResourcesOnly:=True, AllowsBackgroundLoading:=True)>
<Guid(Guids.PackageGuid)>
Public NotInheritable Class Temp_File_Cleaner_VS2026_Package
    Inherits AsyncPackage

    ''' <summary>Identifies this Visual Studio package.</summary>
    Public Const PackageGuidString As String = "2a7a33bf-2fe1-4dfd-9865-a392972e6f73"
    Private ReadOnly logFilePath As String = Path.Combine(Path.GetTempPath(),
        "AutoTempFileCleaner_VS2026_" & Date.UtcNow.ToFileTimeUtc().ToString(Globalization.CultureInfo.InvariantCulture) & ".log")
    Private ReadOnly textService As New TextWriterService()
    Private leaseCoordinator As VsInstanceLeaseCoordinator

    ''' <summary>Registers the service and this participating process.</summary>
    Protected Overrides Async Function InitializeAsync(cancellationToken As CancellationToken,
                                                       progress As IProgress(Of ServiceProgressData)) As Task
        Await MyBase.InitializeAsync(cancellationToken, progress).ConfigureAwait(False)
        Await Me.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken)
        Me.AddService(GetType(ISTextWriterService), AddressOf Me.CreateTextWriterServiceAsync, promote:=False)
        cancellationToken.ThrowIfCancellationRequested()
        Me.leaseCoordinator = New VsInstanceLeaseCoordinator()
        Try
            Me.leaseCoordinator.RegisterLease()
        Catch ex As Exception
            Report("REGISTRATION FAILED; CLEANUP DISABLED: " & ex.Message)
        End Try
    End Function

    ''' <summary>Returns the shared writer using the shell's required callback signature.</summary>
    Public Async Function CreateTextWriterServiceAsync(container As IAsyncServiceContainer,
                                                       cancellationToken As CancellationToken,
                                                       serviceType As Type) As Task(Of Object)
        If container Is Nothing Then Throw New ArgumentNullException(NameOf(container))
        If serviceType IsNot GetType(ISTextWriterService) Then Throw New ArgumentException("Unsupported service type.", NameOf(serviceType))
        Await textService.InitializeAsync(cancellationToken).ConfigureAwait(False)
        Return textService
    End Function

    ''' <summary>Defers cleanup until disposal; a cancellable QueryClose does not release the lease.</summary>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso leaseCoordinator IsNot Nothing Then
                Try
                    leaseCoordinator.UnregisterLeaseAndRunIfLast(
                        Sub()
                            Dim runner As New CleanupRunner(Path.GetTempPath(),
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), logFilePath, AddressOf Report)
                            runner.Run()
                        End Sub)
                Catch ex As Exception
                    Report("CLEANUP FAILED: " & ex.Message)
                Finally
                    Try
                        leaseCoordinator.Dispose()
                    Catch ex As Exception
                        Report("LEASE RELEASE FAILED: " & ex.Message)
                    End Try
                    leaseCoordinator = Nothing
                End Try
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    ''' <summary>Writes without requiring a UI continuation; log failures remain observable in diagnostics.</summary>
    Private Sub Report(message As String)
        Try
            textService.WriteLineAsync(logFilePath, message).GetAwaiter().GetResult()
        Catch ex As Exception
            Trace.TraceError("Temp File Cleaner: {0}; logging failed: {1}", message, ex.Message)
        End Try
    End Sub
End Class
