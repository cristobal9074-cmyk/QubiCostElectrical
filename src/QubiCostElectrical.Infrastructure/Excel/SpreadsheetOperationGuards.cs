using System;
using System.IO;

namespace QubiCostElectrical.Infrastructure;

internal static class SpreadsheetOperationGuards
{
    public static T Execute<T>(ProjectOperationKind kind, Func<T> action, string? invalidMessage = null)
    {
        try
        {
            return action();
        }
        catch (ProjectOperationException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new ProjectOperationException(kind, OperationFailureMessages.ToUserMessage(ex), ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ProjectOperationException(kind, OperationFailureMessages.ToUserMessage(ex), ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new ProjectOperationException(kind, invalidMessage ?? ex.Message, ex);
        }
        catch (InvalidDataException ex)
        {
            throw new ProjectOperationException(kind, invalidMessage ?? ex.Message, ex);
        }
    }

    public static void Execute(ProjectOperationKind kind, Action action, string? invalidMessage = null)
        => Execute(kind, () => { action(); return true; }, invalidMessage);
}
