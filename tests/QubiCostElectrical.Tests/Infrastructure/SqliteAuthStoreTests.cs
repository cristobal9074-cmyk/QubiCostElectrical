using System;
using System.Globalization;
using System.IO;
using System.Threading;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

// Regresión de bug encontrado buscando bugs preexistentes en esta sesión: las fechas se
// guardan con DateTime.ToString("O") (round-trip, invariante) pero se leían con
// DateTime.Parse(string) sin cultura explícita — eso usa CultureInfo.CurrentCulture,
// incluido su calendario. Con un calendario no gregoriano como predeterminado del sistema
// (ej. th-TH, calendario budista), el parseo de un string ISO round-trip lanzaba
// FormatException o leía el año mal. Ahora usa ParseStoredDate (CultureInfo.InvariantCulture
// + DateTimeStyles.RoundtripKind).
public sealed class SqliteAuthStoreTests
{
    [Fact]
    public void CreateUser_ThenFindByEmail_RoundTripsCreatedAt_UnderNonGregorianCulture()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            // th-TH usa el calendario budista tailandés (año = gregoriano + 543) como
            // predeterminado — el caso real que rompía DateTime.Parse sin cultura explícita.
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");

            var store = StoreEnMemoria();
            var createdAt = new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc);
            var user = new UserAccount
            {
                Email = "test@example.com",
                PinHash = "hash",
                DisplayName = "Usuario de prueba",
                CreatedAt = createdAt
            };

            var ex = Record.Exception(() => store.CreateUser(user));
            Assert.Null(ex);

            var found = store.FindByEmail("test@example.com");
            Assert.NotNull(found);
            Assert.Equal(createdAt, found!.CreatedAt);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void GetAllUsers_MultipleRows_ParsesCreatedAtCorrectly()
    {
        var store = StoreEnMemoria();
        store.CreateUser(new UserAccount { Email = "a@example.com", PinHash = "h", DisplayName = "A" });
        store.CreateUser(new UserAccount { Email = "b@example.com", PinHash = "h", DisplayName = "B" });

        var all = store.GetAllUsers();

        Assert.Equal(2, all.Count);
        Assert.All(all, u => Assert.True(u.CreatedAt > DateTime.MinValue));
    }

    private static SqliteAuthStore StoreEnMemoria()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"test_auth_{Guid.NewGuid():N}.db");
        return new SqliteAuthStore($"Data Source={tempDb}");
    }
}
