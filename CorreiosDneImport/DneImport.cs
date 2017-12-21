using System;
using System.IO;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Diagnostics;
using System.Text;

namespace DneImport
{
    class DneImport
    {
        public SqlConnection Connection { get; set; }
        public SqlCommand Command { get; set; }

        Stopwatch Watch;

        public string ConnectionString = "";
        public string ControlTable = "CorreiosImport";
        public string ZipFilesDir = Directory.GetCurrentDirectory() + "\\..\\Data";
        public string Prefix = "eDNE_";
        public string PrefixMaster = "eDNE_Master_";
        public string PrefixDelta = "eDNE_Delta_Master_";
        public string Sep = "@";

        public List<string> Operations = new List<string>() { "INS", "UPD", "DEL" };

        public int LinesTotal { get; private set; } = 0;
        public int LineCount { get; private set; } = 0;

        public int InsertCount { get; private set; } = 0;
        public int UpdateCount { get; private set; } = 0;
        public int DeleteCount { get; private set; } = 0;
        public int IgnoreCount { get; private set; } = 0;

        public int PercentCompleted { get; private set; } = 0;
        public int LastPercentCompleted { get; private set; } = 0;

        // Mapeamento das tabelas dos correios com as tabelas do sistema
        // usando a documentação inclusa nos pacotes.
        // A ordem listada importa pelo respeito às restrições de integridade
        public static Dictionary<string, string> Mapping = new Dictionary<string, string>()
        {
            { "ECT_PAIS", "CorreiosPais" },
            { "LOG_FAIXA_UF", "CorreiosFaixaUf" },
            { "LOG_FAIXA_UOP", "CorreiosFaixaUop" },
            { "LOG_UNID_OPER", "CorreiosUnidOper"},
            { "LOG_GRANDE_USUARIO", "CorreiosGrandeUsuario" },
            { "LOG_VAR_LOG", "CorreiosVarLog" },
            { "LOG_NUM_SEC", "CorreiosNumSec" },
            { "LOG_LOGRADOURO", "CorreiosLogradouro" },
            { "LOG_FAIXA_BAI", "CorreiosFaixaBairro" },
            { "LOG_VAR_BAI", "CorreiosVarBai" },
            { "LOG_BAIRRO", "CorreiosBairro" },
            { "LOG_FAIXA_CPC", "CorreiosFaixaCpc" },
            { "LOG_CPC", "CorreiosCpc" },
            { "LOG_VAR_LOC", "CorreiosVarLoc" },
            { "LOG_FAIXA_LOC", "CorreiosFaixaLocalidade" },
            { "LOG_LOCALIDADE", "CorreiosLocalidade" }
        };

        // Tabelas que possuem dados que deve ser importados após o campo
        // que indica a operação de delta a ser realizada (INS, UPD ou DEL)
        public List<string> DataAfterOpIndex = new List<string>() { "CorreiosFaixaLocalidade" };

        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="connectionString">Conexão com o banco</param>
        public DneImport(string[] args)
        {
            if (args.Length == 0)
            {
                Help();
                return;
            }
            ConnectionString = args[0];
        }

        /// <summary>
        /// Imprime ajuda
        /// </summary>
        public void Help()
        {
            Console.WriteLine("Modo de uso:");
            Console.WriteLine("");
            Console.WriteLine("    dotnet run <ConnectionString>");
            Console.WriteLine("");
            Console.WriteLine("Exemplo:");
            Console.WriteLine("");
            Console.WriteLine("    dotnet run \"Server=Servidor;Database=Banco;User Id=Usuario;Password=Senha;\"");
            Console.WriteLine("");
            Console.ReadKey();
            System.Environment.Exit(1);
        }

        /// <summary>
        /// Realiza o processo de importação dos dados
        /// </summary>
        public void Run()
        {
            Connect();
            Init();

            var files = GetFilesToProcess();
            if (files.Count() == 0)
            {
                Console.WriteLine("Nenhum arquivo para importar.");
                return;
            }

            TurnOffConstraints();

            PrintProgressHeader();
            foreach (var f in files)
            {
                ProcessFile(f);
            }

            TurnOnConstraints();

        }

        /// <summary>
        /// Desabilita restrições de integridade do banco
        /// </summary>
        public void TurnOffConstraints()
        {
            foreach (var table in Mapping.Values)
            {
                SqlExec($"ALTER TABLE {table} NOCHECK CONSTRAINT ALL");
            }
        }

        /// <summary>
        /// Habilita restrições de integridade do banco
        /// </summary>
        public void TurnOnConstraints()
        {
            foreach (var table in Mapping.Values.Reverse())
            {
                SqlExec($"ALTER TABLE {table} WITH CHECK CHECK CONSTRAINT ALL");
            }
        }

        /// <summary>
        /// Retorna a posição de uma linha que contém a indicação
        /// de operação que deve ser realizada (INS, UPD, DEL)
        /// </summary>
        /// <param name="row">Linha</param>
        /// <returns>Posição ou -1 se inexistente</returns>
        public int GetOpIndex(string line)
        {
            int opIndex = -1;
            List<string> values = line.Split(Sep).ToList();

            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (Operations.Contains(values[i]))
                {
                    opIndex = i;
                    break;
                }
            }

            return opIndex;
        }

        /// <summary>
        /// Processa a importação de um arquivo
        /// </summary>
        /// <param name="file">Arquivo</param>
        public void ProcessFile(string file)
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            ZipFile.ExtractToDirectory(ZipFilesDir + "\\" + file, tmpDir);
            var files = new DirectoryInfo(tmpDir).GetFiles("Delimitado\\*.TXT").ToList();
            files.Sort(new FileMappingComparer());
            files.Reverse();

            foreach (var f in files)
            {
                String table = Mapping[GetTableFromFileName(f.Name)];

                using (var fs = f.OpenRead())
                using (var reader = new StreamReader(fs, Encoding.GetEncoding("iso-8859-1")))
                {
                    String line = line = reader.ReadLine();
                    if (line == null) // arquivo vazio
                        continue;

                    PrintStartProgress(file + " " + f.Name, f.FullName);

                    bool delta = f.Name.Contains("DELTA");
                    List<int> keys = GetTableKeysPositions(table);
                    List<string> columns = GetTableColumns(table);
                    int opIndex = GetOpIndex(line);
                    List<string> result;

                    // Voltao ao início do arquivo para ler novamente a primeira linha
                    reader.DiscardBufferedData();
                    reader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);

                    while ((line = reader.ReadLine()) != null)
                    {
                        var values = ProcessValues(line);

                        if (delta)
                            result = ProcessLineDelta(table, opIndex, keys, columns, values);
                        else
                            result = ProcessLineMaster(table, columns, values);

                        if (result[0] == "INS" && SqlExists(table, keys, columns, values))
                        {
                            Increment("IGN");
                            PrintCurrentProgress();
                            continue;
                        }

                        try
                        {
                            SqlExec(result[1]);
                            Increment(result[0]);
                        }
                        catch(SqlException e)
                        {
                            if (e.Message.Contains("Cannot insert duplicate"))
                            {
                                Increment("IGN");
                            }
                            else
                            {
                                TurnOnConstraints();
                                Console.WriteLine();
                                Console.WriteLine($"Line: {line}");
                                Console.WriteLine($"Query: {result[1]}");
                                throw e;
                            }
                        }

                        PrintCurrentProgress();
                    }
                    PrintEndProgress();
                }
            }
            
            // Marca como importado
            SqlExec($"INSERT INTO {ControlTable}(Arquivo, Data) VALUES('{file}', CURRENT_TIMESTAMP)");
            
            // Remove diretório dos arquivos extraídos
            Directory.Delete(tmpDir, true);
        }

        /// <summary>
        /// Verifica se um registro existe no banco
        /// </summary>
        /// <param name="table">Tabela</param>
        /// <param name="keys">Posições das chaves</param>
        /// <param name="columns">Colunas</param>
        /// <param name="values">Valores</param>
        /// <returns>Verdadeiro se existe</returns>
        public bool SqlExists(string table, List<int> keys, List<string> columns, List<string> values)
        {
            var sql = $"SELECT 1 FROM {table} {GetWhereClause(keys, columns, values)}";
            if (SqlField(sql) != null)
                return true;
            return false;
        }

        /// <summary>
        /// Incrementa contagens das operações de importação
        /// </summary>
        /// <param name="op">Operação</param>
        public void Increment(string op)
        {
            switch(op) {
                case ("INS"): InsertCount++; break;
                case ("UPD"): UpdateCount++; break;
                case ("DEL"): DeleteCount++; break;
                case ("IGN"): IgnoreCount++; break;
                default: break;
            }
        }

        /// <summary>
        /// Processa cada campo para ser inserido em consultas SQL
        /// </summary>
        /// <param name="line">Linha do arquivo</param>
        /// <returns>Lista de campos formatados</returns>
        public List<string> ProcessValues(string line)
        {
            var values = line.Split(Sep);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = values[i].Replace("'", "''").Trim();
                values[i] = values[i].Length == 0 ? "NULL" : "'" + values[i] + "'";
            }
            return values.ToList();
        }

        /// <summary>
        /// Processa linhas master para inserção
        /// </summary>
        /// <param name="table">Tabela para inserir os dados</param>
        /// <param name="columns">Nomes das colunas</param>
        /// <param name="values">Valores dos campos</param>
        /// <returns>SQL de inserção</returns>
        public List<string> ProcessLineMaster(string table, List<string> columns, List<string> values)
        {
            return new List<string>() { "INS", $"INSERT INTO {table} ({String.Join(",", columns)}) VALUES ({String.Join(",", values)})" };
        }

        /// <summary>
        /// Determina o tipo de operação realizada pelas linhas dos arquivos
        /// e as executa
        /// </summary>
        /// <param name="table">Tabela a ser atualizada</param>
        /// <param name="opIndex">Índice do campo que indica a
        /// operação que deve ser realizada</param>
        /// <param name="columns">Nomes das colunas</param>
        /// <param name="values">Valores dos campos</param>
        /// <returns>SQL da operação</returns>
        public List<string> ProcessLineDelta(string table, int opIndex, List<int> keys,
            List<string> columns, List<string> values)
        {
            // "Corrige" os dados das tabelas que não respeitam o padrão
            // de manter a operação como último índice
            if (DataAfterOpIndex.Contains(table))
            {
                var v = values[opIndex];
                values.RemoveAt(opIndex);
                values.Add(v);
                opIndex = values.Count() - 1;
            }
            string sql = null;
            var op = values[opIndex].Replace("'", "");
            values = values.GetRange(0, opIndex);

            if (op == "INS")
            {
                var r = ProcessLineMaster(table, columns, values);
                sql = r[1];
            }
            else if (op == "UPD")
            {
                sql = $"UPDATE {table} {GetSetClause(opIndex, columns, values)} {GetWhereClause(keys, columns, values)}";
            }
            else if (op == "DEL")
            {
                sql = $"DELETE FROM {table} {GetWhereClause(keys, columns, values)}";
            }

            return new List<string>() { op, sql };
        }

        /// <summary>
        /// Constrói uma cláusula SET para ser usado em UPDATEs
        /// </summary>
        /// <param name="opIndex">Índice da operação</param>
        /// <param name="columns">Colunas</param>
        /// <param name="values">Valores</param>
        /// <returns>Cláusula SET</returns>
        public string GetSetClause(int opIndex, List<string> columns, List<string> values)
        {
            var set = new List<string>();
            for (int i = 0; i <= opIndex - 1; i++)
            {
                set.Add($"{columns[i]}={values[i]}");
            }
            return $"SET {String.Join(",", set)}";
        }

        /// <summary>
        /// Constróia uma cláusula WHERE para ser usado em
        /// UPDATEs e DELETEs
        /// </summary>
        /// <param name="keys">Posições das chaves</param>
        /// <param name="columns">Colunas</param>
        /// <param name="values">Valores</param>
        /// <returns>Cláusula WHERE</returns>
        public string GetWhereClause(List<int> keys, List<string> columns, List<string> values)
        {
            var where = new List<string>();
            foreach (var k in keys)
            {
                where.Add($"{columns[k]}={values[k]}");
            }
            return $"WHERE {String.Join(" AND ", where)}";
        }

        /// <summary>
        /// Retorna os campos de uma tabela
        /// </summary>
        /// <param name="table">Tabela</param>
        /// <returns>Lista de nomes dos campos</returns>
        public List<string> GetTableColumns(string table)
        {
            return SqlScalar($@"SELECT c.COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_NAME = '{table}'
                ORDER BY c.ORDINAL_POSITION ASC");
        }

        /// <summary>
        /// Retorna as posições das chaves de uma tabela
        /// </summary>
        /// <param name="table">Tabela</param>
        /// <returns>Lista de posições das chaves</returns>
        public List<int> GetTableKeysPositions(string table)
        {
            return SqlScalar($@"SELECT c.ORDINAL_POSITION - 1
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
				JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu
					ON tc.CONSTRAINT_NAME = ccu.CONSTRAINT_NAME
                JOIN INFORMATION_SCHEMA.COLUMNS c
                    ON tc.TABLE_NAME = c.TABLE_NAME
					AND ccu.COLUMN_NAME = c.COLUMN_NAME
                WHERE tc.TABLE_NAME = '{table}'
                    AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ORDER BY c.ORDINAL_POSITION ASC")
                .Select(int.Parse)
                .ToList();
        }

        public void PrintProgressHeader()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("{0,0} {1,83} {2,8} {3,9} {4,11} {5,9} {6,9}",
                "Arquivo", "Tempo", "Total", "Inseridos", "Atualizados", "Removidos", "Ignorados");
        }

        /// <summary>
        /// Imprime início da barra de progresso
        /// </summary>
        public void PrintStartProgress(string item, string fullFileName)
        {
            Watch = System.Diagnostics.Stopwatch.StartNew();
            var regex = new Regex(@".* (ECT|LOG|DELTA)_(?<Name>[A-Z_]*).TXT");
            var txt = regex.Match(item).Groups["Name"].Value;
            var itemName = (item.Contains(PrefixDelta) ? "DELTA " : "MASTER") +
                $" {FileNameComparer.GetDateFromFileName(item)} {txt}";
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(itemName);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("{0," + (33 - itemName.Length).ToString() + "}", "|");
            LineCount = LastPercentCompleted =
                InsertCount = UpdateCount = DeleteCount = IgnoreCount = 0;
            LinesTotal = File.ReadLines(fullFileName).Count();
        }

        /// <summary>
        /// Imprime caractere da barra de progresso da importação
        /// </summary>
        public void PrintCurrentProgress()
        {
            LineCount++;
            PercentCompleted = LineCount * 100 / (LinesTotal == 0 ? 1 : LinesTotal);
            PercentCompleted = (PercentCompleted > 100) ? 100 : PercentCompleted;
            if ((PercentCompleted % 2 == 0 && (PercentCompleted > LastPercentCompleted)) || PercentCompleted == 100)
            {
                for (int i = LastPercentCompleted; i < PercentCompleted; i = i + 2)
                {
                    Console.Write("=");
                }
                LastPercentCompleted = PercentCompleted;
            }
        }

        /// <summary>
        /// Imprime resumo do progresso de importação
        /// </summary>
        public void PrintEndProgress()
        {
            PercentCompleted = 100;
            PrintCurrentProgress();
            Watch.Stop();
            Console.Write("|");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("{0,7} {1,8} {2,9} {3,11} {4,9} {5,9}",
                FormatTime(Watch.ElapsedMilliseconds), LineCount - 1, InsertCount, UpdateCount, DeleteCount, IgnoreCount);
        }

        /// <summary>
        /// Formata milisegundos em NhNmNs ou Nms
        /// </summary>
        public static string FormatTime(long milliseconds)
        {
            TimeSpan t = TimeSpan.FromMilliseconds(milliseconds);
            string formatted = "";
            if (t.Hours > 0) { formatted += String.Format("{0}h", t.Hours); }
            if (t.Hours + t.Minutes > 0) { formatted += String.Format("{0}m", t.Minutes); }
            if (t.Hours + t.Minutes + t.Seconds > 0) { formatted += String.Format("{0}s", t.Seconds); }
            else { formatted += String.Format("{0}ms", t.Milliseconds); }
            return formatted;
        }

        /// <summary>
        /// Prepara conexão com o banco de dados
        /// </summary>
        public void Connect()
        {
            Connection = new SqlConnection(ConnectionString);
            Connection.Open();
            Command = Connection.CreateCommand();
            Command.CommandTimeout = 0;
            Command.Connection = Connection;
        }

        /// <summary>
        /// Executa um comando no banco de dados
        /// </summary>
        /// <param name="cmd">Comando</param>
        public void SqlExec(string cmd)
        {
            Command.CommandText = cmd;
            Command.ExecuteNonQuery();
        }

        /// <summary>
        /// Executa um `SELECT` que retorna um único campo no banco de dados
        /// </summary>
        /// <param name="cmd">Comando</param>
        /// <returns>Campo de resultado</returns>
        public String SqlField(string cmd)
        {
            var c = SqlScalar(cmd);
            if (c.Count > 0)
            {
                return c.First().ToString();
            }
            return null;
        }

        /// <summary>
        /// Retorna um `SELECT` de coluna como lista
        /// </summary>
        /// <param name="cmd">Comando</param>
        /// <returns>Lista de resultados</returns>
        public List<string> SqlScalar(string cmd)
        {
            Command.CommandText = cmd;
            SqlDataReader reader = Command.ExecuteReader();
            int columns = reader.FieldCount;

            List<string> resultList = new List<string>();
            while (reader.Read())
            {
                resultList.Add(reader.GetValue(0).ToString());
            }
            reader.Dispose();

            return resultList;
        }

        /// <summary>
        /// Inicializa a aplicação e cria suas tabelas se necessário
        /// </summary>
        public void Init()
        {
            // Tabela de controle das importações
            var cmd = $"SELECT 1 FROM sysobjects WHERE name = '{ControlTable}' AND xtype = 'U'";
            if (SqlField(cmd) == null)
            {
                SqlExec(File.ReadAllText(Directory.GetCurrentDirectory() + "/Sql/ImportSchema.sql"));
            }

            // Tabelas de dados importados
            cmd = $"SELECT 1 FROM sysobjects WHERE name LIKE 'Correios%' AND name <> '{ControlTable}' AND xtype = 'U'";
            if (SqlField(cmd) == null)
            {
                SqlExec(File.ReadAllText(Directory.GetCurrentDirectory() + "/Sql/CorreiosSchema.sql"));
            }
        }
        
        /// <summary>
        /// Identifica a tabela de importação para um arquivo 
        /// </summary>
        /// <param name="file">Arquivo</param>
        /// <returns>Tabela correspondente</returns>
        public static string GetTableFromFileName(String file)
        {
            var regex = new Regex(@".*(?<Table>(ECT|LOG)_.*)\.TXT");
            var result = regex.Match(file);
            try
            {
                return Mapping.Keys.Where(k => result.Groups["Table"].Value.Contains(k)).First();
            }
            catch
            {
                throw new Exception($"Não foi possível identificar uma tabela para o arquivo {file}");
            }
        }

        /// <summary>
        /// Arquivos no diretório na ordem em que devem ser importados
        /// </summary>
        /// <returns>Lista de arquivos</returns>
        public List<String> GetAllFiles()
        {
            var files = new List<String>() { };
            var d = new DirectoryInfo(ZipFilesDir);
            foreach (var f in d.GetFiles(Prefix + "*.zip"))
            {
                files.Add(f.Name);
            }
            files.Sort(new FileNameComparer());
            return files;
        }

        /// <summary>
        /// Verifica o banco de dados e os arquivos existentes
        /// para decidir quais arquivos devem ser importados
        /// </summary>
        /// <returns>Arquivos não importados</returns>
        public List<String> GetFilesToProcess()
        {
            var files = new List<String>();
            var comparer = new FileNameComparer();
            var lastMaster = SqlField($"SELECT TOP(1) Arquivo FROM {ControlTable} WHERE Arquivo LIKE '{PrefixMaster}%' ORDER BY Arquivo DESC");
            var processedFiles = SqlScalar($"SELECT Arquivo FROM {ControlTable}");
            foreach (var f in GetAllFiles())
            {
                // Arquivo já importado
                if (processedFiles.Contains(f))
                    continue;

                // Se menor que o último master importado, desconsidera
                if (lastMaster != null && comparer.Compare(f, lastMaster) < 0)
                    continue;

                // Se novo arquivo master, desconsidera tudo e parte dele
                if (f.Contains(PrefixMaster))
                    files.Clear();

                files.Add(f);
            }

            if (null == lastMaster && files.Where(s => s.Contains(PrefixMaster)).Count() < 1)
            {
                throw new Exception("Nenhum arquivo 'Master' de referência foi encontrado em importações" +
                    "anteriores ou em arquivo físico. Não é possível continuar.");
            }

            return files;
        }
    }
}
