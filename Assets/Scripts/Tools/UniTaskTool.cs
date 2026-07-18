using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Tools
{
    /// <summary>
    /// Safe boundary for Unity callbacks that cannot await an asynchronous operation.
    /// Expected lifetime cancellation is ignored; unexpected failures retain operation context.
    /// </summary>
    public static class UniTaskTool
    {
        public static void ForgetLogged(this UniTask task, string context) =>
            task.Forget(exception => LogUnexpected(exception, context));

        public static void ForgetLogged<T>(this UniTask<T> task, string context) =>
            task.Forget(exception => LogUnexpected(exception, context));

        private static void LogUnexpected(Exception exception, string context)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }

            Debug.LogError($"{context} failed unexpectedly.\n{exception}");
        }
    }
}
