using System;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.States
{
    public static class ProcessStateMachine
    {
        public static bool IsValidTransition(ProcessState fromState, ProcessState toState)
        {
            if (fromState == toState) return true;

            return fromState switch
            {
                ProcessState.Created => toState == ProcessState.Starting || toState == ProcessState.Unknown,
                ProcessState.Starting => toState == ProcessState.Running || toState == ProcessState.Stopped || toState == ProcessState.Crashed || toState == ProcessState.Unknown,
                ProcessState.Running => toState == ProcessState.Stopping || toState == ProcessState.Stopped || toState == ProcessState.Crashed || toState == ProcessState.Unknown,
                ProcessState.Stopping => toState == ProcessState.Stopped || toState == ProcessState.Crashed || toState == ProcessState.Unknown,
                ProcessState.Stopped => toState == ProcessState.Unknown,
                ProcessState.Crashed => toState == ProcessState.Unknown,
                ProcessState.Unknown => true, // Unknown is a fallback/wildcard state
                _ => false
            };
        }

        public static void ValidateTransition(ProcessState fromState, ProcessState toState)
        {
            if (!IsValidTransition(fromState, toState))
            {
                throw new InvalidOperationException($"Invalid process state transition from '{fromState}' to '{toState}'.");
            }
        }
    }
}
