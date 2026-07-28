using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// Comprehensive unit and integration test suite verifying Phase 6 Part 5: Installation Engine.
    /// Covers FSM transitions, Windows Restart Manager, Atomic file replacement, Validation, and Coordination.
    /// </summary>
    public class UpdatePlatformPart5Tests
    {
        #region Installation State Machine Tests

        [Fact]
        public void StateMachine_ShouldInitializeToIdle()
        {
            var fsm = new InstallationStateMachine();
            Assert.Equal(InstallationState.Idle, fsm.CurrentState);
        }

        [Fact]
        public void StateMachine_ShouldTransitionCorrectlyThroughHappyPath()
        {
            var fsm = new InstallationStateMachine();

            fsm.TransitionTo(InstallationState.Preparing);
            Assert.Equal(InstallationState.Preparing, fsm.CurrentState);

            fsm.TransitionTo(InstallationState.Validating);
            Assert.Equal(InstallationState.Validating, fsm.CurrentState);

            fsm.TransitionTo(InstallationState.Staging);
            Assert.Equal(InstallationState.Staging, fsm.CurrentState);

            fsm.TransitionTo(InstallationState.StoppingServices);
            Assert.Equal(InstallationState.StoppingServices, fsm.CurrentState);

            fsm.TransitionTo(InstallationState.Installing);
            Assert.Equal(InstallationState.Installing, fsm.CurrentState);

            fsm.TransitionTo(InstallationState.Verifying);
            Assert.Equal(InstallationState.Verifying, fsm.CurrentState);

            fsm.TransitionTo(InstallationState.Restarting);
            Assert.Equal(InstallationState.Restarting, fsm.CurrentState);

            fsm.TransitionTo(InstallationState.Completed);
            Assert.Equal(InstallationState.Completed, fsm.CurrentState);
        }

        [Fact]
        public void StateMachine_ShouldRaiseStateChangedEvent()
        {
            var fsm = new InstallationStateMachine();
            InstallationState? oldState = null;
            InstallationState? newState = null;

            fsm.StateChanged += (sender, e) =>
            {
                oldState = e.OldState;
                newState = e.NewState;
            };

            fsm.TransitionTo(InstallationState.Preparing);

            Assert.Equal(InstallationState.Idle, oldState);
            Assert.Equal(InstallationState.Preparing, newState);
        }

        [Fact]
        public void StateMachine_ShouldThrowOnInvalidTransitions()
        {
            var fsm = new InstallationStateMachine();

            // Direct jump from Idle to Installing is invalid
            Assert.Throws<InstallationFailedException>(() => fsm.TransitionTo(InstallationState.Installing));
        }

        [Fact]
        public void StateMachine_ShouldAlwaysAllowTransitionToFailedFromNonTerminalStates()
        {
            var fsm = new InstallationStateMachine();
            fsm.TransitionTo(InstallationState.Preparing);
            fsm.TransitionTo(InstallationState.Validating);

            fsm.TransitionTo(InstallationState.Failed);
            Assert.Equal(InstallationState.Failed, fsm.CurrentState);
        }

        [Fact]
        public void StateMachine_ShouldDisallowTransitionToFailedFromCompleted()
        {
            var fsm = new InstallationStateMachine();
            fsm.TransitionTo(InstallationState.Preparing);
            fsm.TransitionTo(InstallationState.Validating);
            fsm.TransitionTo(InstallationState.Staging);
            fsm.TransitionTo(InstallationState.StoppingServices);
            fsm.TransitionTo(InstallationState.Installing);
            fsm.TransitionTo(InstallationState.Verifying);
            fsm.TransitionTo(InstallationState.Restarting);
            fsm.TransitionTo(InstallationState.Completed);

            Assert.Throws<InstallationFailedException>(() => fsm.TransitionTo(InstallationState.Failed));
        }

        #endregion

        #region Windows Restart Manager Tests

        [Fact]
        public void RestartManager_ShouldBeResilientAndNotThrowOnEmptyInput()
        {
            var rm = new WindowsRestartManager();
            var locks = rm.DetectFileLocks(new string[0]);
            Assert.Empty(locks);

            bool shutdownSuccess = rm.ShutdownApplications(new string[0]);
            Assert.True(shutdownSuccess);

            bool restartSuccess = rm.RestartApplications();
            Assert.True(restartSuccess);
        }

        [Fact]
        public void RestartManager_ShouldBeResilientWhenDetectingLocksOnNonExistentFiles()
        {
            var rm = new WindowsRestartManager();
            var locks = rm.DetectFileLocks(new[] { "C:\\NonExistentFile_" + Guid.NewGuid() });
            Assert.Empty(locks);
        }

        #endregion

        #region Atomic File Replacer Tests

        [Fact]
        public void AtomicReplacer_ShouldAtomicallyWriteNewFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SAYRA_Test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                string targetFile = Path.Combine(tempDir, "production.txt");
                string replacementFile = Path.Combine(tempDir, "staged.txt");

                File.WriteAllText(replacementFile, "Updated content");

                var replacer = new AtomicFileReplacer();
                replacer.ReplaceFile(targetFile, replacementFile);

                Assert.True(File.Exists(targetFile));
                Assert.Equal("Updated content", File.ReadAllText(targetFile));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void AtomicReplacer_ShouldCreateBackupOfOriginalFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SAYRA_Test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                string targetFile = Path.Combine(tempDir, "production.txt");
                string replacementFile = Path.Combine(tempDir, "staged.txt");
                string backupFile = Path.Combine(tempDir, "backup.txt");

                File.WriteAllText(targetFile, "Original content");
                File.WriteAllText(replacementFile, "Updated content");

                var replacer = new AtomicFileReplacer();
                replacer.ReplaceFile(targetFile, replacementFile, backupFile);

                Assert.True(File.Exists(targetFile));
                Assert.Equal("Updated content", File.ReadAllText(targetFile));

                Assert.True(File.Exists(backupFile));
                Assert.Equal("Original content", File.ReadAllText(backupFile));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void AtomicReplacer_ShouldPreserveConfigurationsDuringDirectorySwap()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SAYRA_Test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                string sourceDir = Path.Combine(tempDir, "staging");
                string targetDir = Path.Combine(tempDir, "production");
                string backupDir = Path.Combine(tempDir, "backup");

                Directory.CreateDirectory(sourceDir);
                Directory.CreateDirectory(targetDir);
                Directory.CreateDirectory(backupDir);

                // Create staging files
                File.WriteAllText(Path.Combine(sourceDir, "app.dll"), "New app binary");
                File.WriteAllText(Path.Combine(sourceDir, "client_config.json"), "New default config"); // This should be SKIPPED from copy to preserve local config!

                // Create active target production files
                File.WriteAllText(Path.Combine(targetDir, "app.dll"), "Old app binary");
                File.WriteAllText(Path.Combine(targetDir, "client_config.json"), "Customized local configuration");

                var replacer = new AtomicFileReplacer();
                replacer.ReplaceDirectoryContents(sourceDir, targetDir, backupDir);

                // Verify app.dll was replaced and backed up
                Assert.Equal("New app binary", File.ReadAllText(Path.Combine(targetDir, "app.dll")));
                Assert.Equal("Old app binary", File.ReadAllText(Path.Combine(backupDir, "app.dll")));

                // Verify client_config.json was PRESERVED (skipped from overwriting)
                Assert.Equal("Customized local configuration", File.ReadAllText(Path.Combine(targetDir, "client_config.json")));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        #endregion

        #region Installation Validator Tests

        [Fact]
        public async Task Validator_ShouldSucceedWhenAllFilesAreIdentical()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SAYRA_Test_" + Guid.NewGuid());
            string stagingDir = Path.Combine(tempDir, "staging");
            string targetDir = Path.Combine(tempDir, "production");
            string backupDir = Path.Combine(tempDir, "backup");

            Directory.CreateDirectory(stagingDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(backupDir);

            try
            {
                File.WriteAllText(Path.Combine(stagingDir, "test.dll"), "Binary contents");
                File.WriteAllText(Path.Combine(targetDir, "test.dll"), "Binary contents");

                var job = new InstallationJob { Package = new UpdatePackage { Version = "2.0.0" } };
                var context = new InstallationContext(job, stagingDir, targetDir, backupDir, CancellationToken.None);

                var validator = new InstallationValidator();
                bool result = await validator.ValidateAsync(context);

                Assert.True(result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task Validator_ShouldThrowExceptionWhenFileIsMissingFromTarget()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SAYRA_Test_" + Guid.NewGuid());
            string stagingDir = Path.Combine(tempDir, "staging");
            string targetDir = Path.Combine(tempDir, "production");
            string backupDir = Path.Combine(tempDir, "backup");

            Directory.CreateDirectory(stagingDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(backupDir);

            try
            {
                File.WriteAllText(Path.Combine(stagingDir, "test.dll"), "Binary contents");
                // test.dll is MISSING from target

                var job = new InstallationJob { Package = new UpdatePackage { Version = "2.0.0" } };
                var context = new InstallationContext(job, stagingDir, targetDir, backupDir, CancellationToken.None);

                var validator = new InstallationValidator();
                await Assert.ThrowsAsync<InstallationValidationException>(() => validator.ValidateAsync(context));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task Validator_ShouldThrowExceptionWhenHashIsMismatched()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SAYRA_Test_" + Guid.NewGuid());
            string stagingDir = Path.Combine(tempDir, "staging");
            string targetDir = Path.Combine(tempDir, "production");
            string backupDir = Path.Combine(tempDir, "backup");

            Directory.CreateDirectory(stagingDir);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(backupDir);

            try
            {
                File.WriteAllText(Path.Combine(stagingDir, "test.dll"), "Staged binary content");
                File.WriteAllText(Path.Combine(targetDir, "test.dll"), "Corrupted target content");

                var job = new InstallationJob { Package = new UpdatePackage { Version = "2.0.0" } };
                var context = new InstallationContext(job, stagingDir, targetDir, backupDir, CancellationToken.None);

                var validator = new InstallationValidator();
                await Assert.ThrowsAsync<InstallationValidationException>(() => validator.ValidateAsync(context));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        #endregion

        #region Installation Coordinator Tests

        [Fact]
        public async Task Coordinator_ShouldCoordinateFullHappyPathProcess()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SAYRA_Test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                string packageZipPath = Path.Combine(tempDir, "package.zip");

                // Create a real update ZIP package containing files to install
                using (var zipFileStream = new FileStream(packageZipPath, FileMode.Create))
                {
                    using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
                    {
                        var entry = archive.CreateEntry("game.dll");
                        using (var entryStream = entry.Open())
                        using (var writer = new StreamWriter(entryStream))
                        {
                            writer.Write("New game executable logic");
                        }
                    }
                }

                var job = new InstallationJob
                {
                    Package = new UpdatePackage { Version = "2.0.0" },
                    PackagePath = packageZipPath
                };

                Func<IInstallationStateMachine> fsmFactory = () => new InstallationStateMachine();
                var rm = new WindowsRestartManager();
                var replacer = new AtomicFileReplacer();
                var validator = new InstallationValidator();

                var coordinator = new InstallationCoordinator(fsmFactory, rm, replacer, validator);
                var progressValues = new List<double>();
                var progressReporter = new SynchronousProgress<double>(val => progressValues.Add(val));

                var result = await coordinator.CoordinateAsync(job, progressReporter, CancellationToken.None);

                Assert.True(result.Success, result.ErrorMessage);
                Assert.Equal(InstallationState.Completed, job.State);
                Assert.Contains(10.0, progressValues);
                Assert.Contains(100.0, progressValues);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        #endregion
    }

    /// <summary>
    /// A synchronous progress reporter useful for deterministic unit testing.
    /// </summary>
    public class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Report(T value)
        {
            _handler(value);
        }
    }
}
