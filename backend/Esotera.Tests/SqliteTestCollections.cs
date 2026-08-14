namespace Esotera.Tests;

/// <summary>
/// SQLite in-memory compartilhado não tolera testes paralelos no mesmo host
/// (lock + FakeMercadoPagoClient singleton).
/// </summary>
[CollectionDefinition("sqlite-j3-webhook", DisableParallelization = true)]
public sealed class SqliteJ3WebhookCollection : ICollectionFixture<SqliteWebApplicationFactory>;

[CollectionDefinition("sqlite-j3-e2e", DisableParallelization = true)]
public sealed class SqliteJ3E2ECollection : ICollectionFixture<SqliteJ3FulfillmentEnabledWebApplicationFactory>;

[CollectionDefinition("sqlite-j3-pending-fail", DisableParallelization = true)]
public sealed class SqliteJ3PendingFailCollection : ICollectionFixture<SqliteJ3FulfillmentInsertFailsWebApplicationFactory>;
