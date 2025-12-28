using System.ComponentModel;
using System.Data;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.SemanticKernel;

namespace SemanticKernelPoC.Plugins
{
    public sealed class DummyDbPlugin
    {
        [KernelFunction("get_client_transactions")]
        [Description(@"
Возвращает только фактические записи финансовых транзакций клиента в пределах параметров запроса.
Метод предоставляет неизменённые первичные данные в режиме только для чтения и не добавляет интерпретаций, выводов или обобщений.
Результат не содержит оценок, прогнозов или рекомендаций.
Любая аналитическая обработка данных должна выполняться ассистентом в соответствии с системными правилами.
Результат возвращается в структурированном машиночитаемом формате (meta + transactions).
")]
        public async Task<GetClientTransactionsResult> GetClientTransactionsAsync(
           [Description("Дата начала периода, включительно (YYYY-MM-DD)")] string? startDate = null,
           [Description("Дата окончания периода, включительно (YYYY-MM-DD)")] string? endDate = null,
           [Description("Максимальное количество возвращаемых записей (положительное целое число)")] int limit = 100)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return await Task.FromResult(
                UseConnection(conn =>
                {
                    var sql = @"

                        select transaction_id,
                               dt,
                               amount,
                               currency,
                               counterparty
                          from transactions
                         where 1 = 1

                    ";

                    var parameters = new List<SqliteParameter>();

                    if (startDate is not null)
                    {
                        sql += " and dt >= @startDate";
                        parameters.Add(new SqliteParameter("@startDate", startDate));
                    }

                    if (endDate is not null)
                    {
                        sql += " and dt <= @endDate";
                        parameters.Add(new SqliteParameter("@endDate", endDate));
                    }

                    sql += " order by dt asc limit @limit";

                    parameters.Add(new SqliteParameter("@limit", limit + 1)); // +1 для has_more

                    using var command = new SqliteCommand(sql, conn);
                    command.Parameters.AddRange(parameters);

                    var result = new GetClientTransactionsResult
                    {
                        Metadata =
                        {
                            Limit = limit
                        }
                    };

                    using var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        if (result.Transactions.Count == limit)
                        {
                            result.Metadata.HasMore = true;
                            break;
                        }

                        result.Transactions.Add(new TransactionInfo
                        {
                            TransactionId = reader.GetString(reader.GetOrdinal("transaction_id")),
                            Dt = reader.GetDateTime(reader.GetOrdinal("dt")).ToString("yyyy-MM-dd"),
                            Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                            Currency = reader.GetString(reader.GetOrdinal("currency")),
                            Counterparty = reader.GetString(reader.GetOrdinal("counterparty"))
                        });
                    }

                    result.Metadata.Returned = result.Transactions.Count;

                    return result;
                })
            );
        }

        public class GetClientTransactionsResult
        {
            [JsonPropertyName("meta")]
            public TransactionsMetadata Metadata { get; } = new();

            [JsonPropertyName("transactions")]
            public List<TransactionInfo> Transactions { get; } = new();
        }

        public class TransactionsMetadata
        {
            [JsonPropertyName("limit")]
            public int Limit { get; set; }

            [JsonPropertyName("returned")]
            public int Returned { get; set; }

            [JsonPropertyName("has_more")]
            public bool HasMore { get; set; }
        }

        public class TransactionInfo
        {
            [JsonPropertyName("transaction_id")]
            public string TransactionId { get; set; } = default!;

            [JsonPropertyName("dt")]
            public string Dt { get; set; } = default!; // ISO-8601 yyyy-MM-dd

            [JsonPropertyName("amount")]
            public decimal Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; } = default!;

            [JsonPropertyName("counterparty")]
            public string Counterparty { get; set; } = default!;
        }

        private T UseConnection<T>(Func<SqliteConnection, T> action)
        {
            using var conn = new SqliteConnection("Data Source=dummy.db");
            try
            {
                return action(conn);
            }
            finally
            {
                if (conn?.State == ConnectionState.Open)
                    conn.Close();
            }
        }
    }

}
