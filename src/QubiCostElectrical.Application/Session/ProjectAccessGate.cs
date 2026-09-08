using System;

namespace QubiCostElectrical;

/// <summary>
/// Gate de acceso RO (lock SMB + rol). Usado solo por <see cref="BudgetProjectSession"/>.
/// </summary>
internal sealed class ProjectAccessGate
{
    private bool lockReadOnly;
    private bool roleReadOnly;
    private string lockReadOnlyReason = "";
    private string roleReadOnlyReason = "";

    public bool IsReadOnly => lockReadOnly || roleReadOnly;

    public string ReadOnlyReason
    {
        get
        {
            if (lockReadOnly && roleReadOnly)
                return $"{roleReadOnlyReason} {lockReadOnlyReason}".Trim();
            if (roleReadOnly)
                return roleReadOnlyReason;
            if (lockReadOnly)
                return lockReadOnlyReason;
            return "";
        }
    }

    public void SetLockReadOnly(bool isReadOnly, string? reason = null)
    {
        lockReadOnly = isReadOnly;
        lockReadOnlyReason = isReadOnly
            ? (string.IsNullOrWhiteSpace(reason) ? "Proyecto abierto en modo solo lectura." : reason.Trim())
            : "";
    }

    public void SetRoleReadOnly(bool isReadOnly, string? reason = null)
    {
        roleReadOnly = isReadOnly;
        roleReadOnlyReason = isReadOnly
            ? (string.IsNullOrWhiteSpace(reason) ? "Tu rol no permite editar." : reason.Trim())
            : "";
    }

    public void EnsureWritable(string operation)
    {
        if (!IsReadOnly)
            return;

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(operation)
                ? "El proyecto está en modo solo lectura."
                : $"El proyecto está en modo solo lectura. No se puede {operation}.");
    }
}
