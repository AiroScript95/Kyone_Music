using Microsoft.Data.Sqlite;
using KyoneMusic.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace KyoneMusic.Services
{
    public class DatabaseService
    {
        /* ESTADO */
        private string _database = string.Empty;

        public DatabaseService()
        {
            string caminhoData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string caminhoPasta = Path.Combine(caminhoData, "Kitsune");
            string caminhoPastaConfig = Path.Combine(caminhoPasta, "Settings");
            Directory.CreateDirectory(caminhoPastaConfig);
            string caminhoDb = Path.Combine(caminhoPastaConfig, "Kitsune.db");

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = caminhoDb,
                ForeignKeys = true,
                DefaultTimeout = 5
            };

            _database = builder.ToString();
        }

        public void IniciarDatabase()
        {
            string comandoMusicas = @"
            CREATE TABLE IF NOT EXISTS Musicas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Caminho TEXT NOT NULL UNIQUE,
                Titulo TEXT,
                Artista TEXT,
                Album TEXT,
                Genero TEXT,
                DuracaoSegundos INTEGER,
                DataModificacao INTEGER NOT NULL,
                Imagem BLOB,
                PrimeiraVezAusente TEXT,
                VezesTocada INTEGER DEFAULT 0,
                Gosto INTEGER DEFAULT 0,
                DataGosto TEXT
            )";

            string comandoPlaylists = @"
                CREATE TABLE IF NOT EXISTS Playlists (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nome TEXT NOT NULL,
                DataCriacao TEXT,
                Direcao INTEGER DEFAULT 0,
                Ordem TEXT DEFAULT 'Manual'
            )";
            string comandoPlaylistMusicas = @"
             CREATE TABLE IF NOT EXISTS PlaylistMusicas (
                 PlaylistId INTEGER NOT NULL,
                 MusicaId INTEGER NOT NULL,
                 DataAdicionado TEXT,
                 FOREIGN KEY (PlaylistId)
                     REFERENCES Playlists(Id)
                     ON DELETE CASCADE,
                 FOREIGN KEY (MusicaId)
                     REFERENCES Musicas(Id)
                     ON DELETE CASCADE,
                 UNIQUE(PlaylistId, MusicaId)
             )";

            string comandoFila = @"
            CREATE TABLE IF NOT EXISTS Fila (
                Posicao INTEGER PRIMARY KEY,
                MusicaId INTEGER NOT NULL,
                FOREIGN KEY (MusicaId)
                    REFERENCES Musicas(Id)
                    ON DELETE CASCADE
            )";

            using (SqliteConnection connection = new(_database)) {
                connection.Open();

                using (SqliteCommand pragma = connection.CreateCommand()) {
                    pragma.CommandText = @"
                    PRAGMA journal_mode = WAL;
                    PRAGMA foreign_keys = ON;
                    PRAGMA busy_timeout = 5000;";
                    pragma.ExecuteNonQuery();
                }

                using (SqliteCommand cmd = connection.CreateCommand()) {
                    cmd.CommandText = comandoMusicas;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = comandoPlaylists;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = comandoPlaylistMusicas;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = comandoFila;
                    cmd.ExecuteNonQuery();

                    MigrarDatabase(connection);

                    using (SqliteCommand indices = connection.CreateCommand()) {
                        indices.CommandText = @"
            CREATE INDEX IF NOT EXISTS IX_Musicas_Gosto
                ON Musicas(Gosto);

            CREATE INDEX IF NOT EXISTS IX_PlaylistMusicas_PlaylistId
                ON PlaylistMusicas(PlaylistId);

            CREATE INDEX IF NOT EXISTS IX_PlaylistMusicas_MusicaId
                ON PlaylistMusicas(MusicaId);";

                        indices.ExecuteNonQuery();
                    }
                }
            }
            GarantirPlaylistFavoritos();
        }

        private void MigrarDatabase(SqliteConnection connection)
        {
            SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = "PRAGMA user_version;";
            int versao = Convert.ToInt32(cmd.ExecuteScalar());

            if (versao < 1) {
                cmd.CommandText = "PRAGMA user_version = 1";
                cmd.ExecuteNonQuery();
            }
        }

        private static string LerTexto(SqliteDataReader reader, int ordinal, string valorPadrao = "")
        {
            return reader.IsDBNull(ordinal) ? valorPadrao : reader.GetString(ordinal);
        }

        private static int LerInt32(SqliteDataReader reader, int ordinal, int valorPadrao = 0)
        {
            return reader.IsDBNull(ordinal)
                ? valorPadrao
                : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static long LerInt64(SqliteDataReader reader, int ordinal, long valorPadrao = 0)
        {
            return reader.IsDBNull(ordinal)
                ? valorPadrao
                : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static string FormatarData(DateTime data)
        {
            return data.ToString("O", CultureInfo.InvariantCulture);
        }

        private static DateTime LerData(string? valor, DateTime valorPadrao = default)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return valorPadrao;

            if (DateTime.TryParseExact(
                valor,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime data))
                return data;

            if (DateTime.TryParse(
                valor,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out data))
                return data;

            if (DateTime.TryParse(
                valor,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out data))
                return data;

            return valorPadrao;
        }

        public List<string> ListarMusicasDaPlaylist(int playlistId)
        {
            List<string> caminhos = new List<string>();

            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT Musicas.Caminho
                    FROM Musicas
                    INNER JOIN PlaylistMusicas ON Musicas.Id = PlaylistMusicas.MusicaId
                    WHERE PlaylistMusicas.PlaylistId = @playlistId
                   ORDER BY PlaylistMusicas.DataAdicionado DESC, PlaylistMusicas.rowid DESC";
                cmd.Parameters.AddWithValue("@playlistId", playlistId);

                using (SqliteDataReader reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        caminhos.Add(reader.GetString(0));
                    }
                }
            }

            return caminhos;
        }

        public List<(int Id, string Caminho)> ListarTodasMusicas()
        {
            List<(int Id, string Caminho)> musicas = new List<(int Id, string Caminho)>();

            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Id, Caminho FROM Musicas";

                using (SqliteDataReader reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        musicas.Add((reader.GetInt32(0), reader.GetString(1)));
                    }
                }
            }

            return musicas;
        }

        public List<Playlist> ListarPlaylists()
        {
            List<Playlist> playlists = new();
            Dictionary<int, int> contagens = ContarMusicasPorPlaylist();

            using (SqliteConnection connection = new(_database)) {
                connection.Open();

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Id, Nome, DataCriacao, Ordem, Direcao FROM Playlists";

                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    int id = LerInt32(reader, 0);
                    string ordem = LerTexto(reader, 3);

                    Playlist playlist = new()
                    {
                        Id = id,
                        Name = LerTexto(reader, 1),
                        DataCriacao = LerData(LerTexto(reader, 2), DateTime.MinValue),
                        Ordem = string.IsNullOrWhiteSpace(ordem) ? "Manual" : ordem,
                        Direcao = LerInt32(reader, 4) != 0,
                        NumMusicas = contagens.TryGetValue(id, out int quantidade)
                            ? quantidade.ToString(CultureInfo.InvariantCulture)
                            : "0"
                    };

                    playlists.Add(playlist);
                }
            }

            return playlists;
        }

        public Dictionary<int, int> ContarMusicasPorPlaylist()
        {
            var resultado = new Dictionary<int, int>();

            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"
                SELECT PlaylistId, COUNT(*)
                FROM PlaylistMusicas
                GROUP BY PlaylistId";

                using (SqliteDataReader reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        int playlistId = reader.GetInt32(0);
                        int quantidade = reader.GetInt32(1);
                        resultado[playlistId] = quantidade;
                    }
                }
            }

            return resultado;
        }

        public void RemoverPlaylist(int playlistId)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                using SqliteTransaction tx = connection.BeginTransaction();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM PlaylistMusicas WHERE PlaylistId = @id";
                cmd.Parameters.AddWithValue("@id", playlistId);
                cmd.Transaction = tx;
                cmd.ExecuteNonQuery();

                cmd.CommandText = "DELETE FROM Playlists WHERE Id = @id";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", playlistId);
                cmd.ExecuteNonQuery();
                tx.Commit();
            }
        }

        public void ReordenarPlaylist(int playlistId, string ordem, bool direcao)
        {
            Debug.WriteLine($"{playlistId},{ordem}, {direcao}");
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE Playlists SET Direcao = @direcao, Ordem = @ordem WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", playlistId);
                cmd.Parameters.AddWithValue("@direcao", direcao);
                cmd.Parameters.AddWithValue("@ordem", ordem);
                cmd.ExecuteNonQuery();
            }
        }

        public void AdicionarMusicaAPlaylist(int playlistId, int musicaId)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO PlaylistMusicas (PlaylistId, MusicaId, DataAdicionado) VALUES (@playlistId, @musicaId, @data)";
                cmd.Parameters.AddWithValue("@playlistId", playlistId);
                cmd.Parameters.AddWithValue("@musicaId", musicaId);
                cmd.Parameters.AddWithValue("@data", FormatarData(DateTime.Now));
                cmd.ExecuteNonQuery();
            }
        }

        public void RemoverMusicaPorId(int musicaId)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                using SqliteTransaction tx = connection.BeginTransaction();
                SqliteCommand cmd = connection.CreateCommand();

                cmd.CommandText = "DELETE FROM Fila WHERE MusicaId = @id";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", musicaId);
                cmd.ExecuteNonQuery();

                cmd.CommandText = "DELETE FROM PlaylistMusicas WHERE MusicaId = @id";
                cmd.Parameters.AddWithValue("@id", musicaId);
                cmd.ExecuteNonQuery();

                cmd.CommandText = "DELETE FROM Musicas WHERE Id = @id";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", musicaId);
                cmd.ExecuteNonQuery();
                tx.Commit();
            }
        }

        public void LimparMusicasOrfas()
        {
            var todas = new List<(int Id, string Caminho, string? PrimeiraAusencia)>();

            using (SqliteConnection connection = new(_database)) {
                connection.Open();

                using SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Id, Caminho, PrimeiraVezAusente FROM Musicas";

                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    todas.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2)));
                }
            }

            var idsParaLimpar = new List<int>();
            var idsParaMarcar = new List<int>();
            var idsParaRemover = new List<int>();
            DateTime agora = DateTime.Now;

            foreach (var musica in todas) {
                if (File.Exists(musica.Caminho)) {
                    if (musica.PrimeiraAusencia != null)
                        idsParaLimpar.Add(musica.Id);

                    continue;
                }

                if (musica.PrimeiraAusencia == null) {
                    idsParaMarcar.Add(musica.Id);
                }
                else if (( agora - LerData(musica.PrimeiraAusencia, agora) ).TotalDays >= 180) {
                    idsParaRemover.Add(musica.Id);
                }
            }

            if (idsParaLimpar.Count == 0 &&
                idsParaMarcar.Count == 0 &&
                idsParaRemover.Count == 0) {
                return;
            }

            using (SqliteConnection connection = new(_database)) {
                connection.Open();

                using SqliteTransaction transaction = connection.BeginTransaction();

                using SqliteCommand limparCmd = connection.CreateCommand();
                limparCmd.Transaction = transaction;
                limparCmd.CommandText = @"
            UPDATE Musicas
            SET PrimeiraVezAusente = NULL
            WHERE Id = @id AND PrimeiraVezAusente IS NOT NULL";
                SqliteParameter limparId = limparCmd.Parameters.Add("@id", SqliteType.Integer);
                limparCmd.Prepare();

                using SqliteCommand marcarCmd = connection.CreateCommand();
                marcarCmd.Transaction = transaction;
                marcarCmd.CommandText = @"
            UPDATE Musicas
            SET PrimeiraVezAusente = @data
            WHERE Id = @id";
                SqliteParameter marcarData = marcarCmd.Parameters.Add("@data", SqliteType.Text);
                SqliteParameter marcarId = marcarCmd.Parameters.Add("@id", SqliteType.Integer);
                marcarCmd.Prepare();

                using SqliteCommand removerFilaCmd = connection.CreateCommand();
                removerFilaCmd.Transaction = transaction;
                removerFilaCmd.CommandText = "DELETE FROM Fila WHERE MusicaId = @id";
                SqliteParameter removerFilaId = removerFilaCmd.Parameters.Add("@id", SqliteType.Integer);
                removerFilaCmd.Prepare();

                using SqliteCommand removerPlaylistCmd = connection.CreateCommand();
                removerPlaylistCmd.Transaction = transaction;
                removerPlaylistCmd.CommandText = "DELETE FROM PlaylistMusicas WHERE MusicaId = @id";
                SqliteParameter removerPlaylistId =
                    removerPlaylistCmd.Parameters.Add("@id", SqliteType.Integer);
                removerPlaylistCmd.Prepare();

                using SqliteCommand removerMusicaCmd = connection.CreateCommand();
                removerMusicaCmd.Transaction = transaction;
                removerMusicaCmd.CommandText = "DELETE FROM Musicas WHERE Id = @id";
                SqliteParameter removerMusicaId =
                    removerMusicaCmd.Parameters.Add("@id", SqliteType.Integer);
                removerMusicaCmd.Prepare();

                foreach (int id in idsParaLimpar) {
                    limparId.Value = id;
                    limparCmd.ExecuteNonQuery();
                }

                string dataAtual = FormatarData(agora);

                foreach (int id in idsParaMarcar) {
                    marcarData.Value = dataAtual;
                    marcarId.Value = id;
                    marcarCmd.ExecuteNonQuery();
                }

                foreach (int id in idsParaRemover) {
                    removerFilaId.Value = id;
                    removerFilaCmd.ExecuteNonQuery();

                    removerPlaylistId.Value = id;
                    removerPlaylistCmd.ExecuteNonQuery();

                    removerMusicaId.Value = id;
                    removerMusicaCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        public void AtualizarOrdemPlaylist(int playlistId, string ordem)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE Playlists SET Ordem = @ordem WHERE Id = @id";
                cmd.Parameters.AddWithValue("@ordem", ordem);
                cmd.Parameters.AddWithValue("@id", playlistId);
                cmd.ExecuteNonQuery();
            }
        }

        public void RemoverMusicaDaPlaylist(int playlistId, int musicaId)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM PlaylistMusicas WHERE PlaylistId = @playlistId AND MusicaId = @musicaId";
                cmd.Parameters.AddWithValue("@playlistId", playlistId);
                cmd.Parameters.AddWithValue("@musicaId", musicaId);
                cmd.ExecuteNonQuery();
            }
        }

        public void IncrementarReproducoes(string caminho)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE Musicas SET VezesTocada = VezesTocada + 1 WHERE Caminho = @caminho";
                cmd.Parameters.AddWithValue("@caminho", caminho);
                cmd.ExecuteNonQuery();
            }
        }

        public bool CriarPlaylist(string playlist)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();

                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT INTO Playlists (Nome, DataCriacao) VALUES (@nome, @data)";
                cmd.Parameters.AddWithValue("@nome", playlist);
                cmd.Parameters.AddWithValue("@data", FormatarData(DateTime.Now));
                cmd.ExecuteNonQuery();
                return true;
            }
        }

        public int ObterIdMusicaPorCaminho(string caminho)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Id FROM Musicas WHERE Caminho = @caminho";
                cmd.Parameters.AddWithValue("@caminho", caminho);

                object? resultado = cmd.ExecuteScalar();
                return resultado != null ? Convert.ToInt32(resultado) : -1;
            }
        }

        public int ObterVezesTocada(string caminho)
        {
            using SqliteConnection connection = new(_database);
            connection.Open();

            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT VezesTocada FROM Musicas WHERE Caminho = @caminho";
            cmd.Parameters.AddWithValue("@caminho", caminho);

            object? resultado = cmd.ExecuteScalar();

            return resultado == null || resultado == DBNull.Value
                ? 0
                : Convert.ToInt32(resultado, CultureInfo.InvariantCulture);
        }

        public string? ObterCaminhoMusicaMaisRecente(int playlistId)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT Musicas.Caminho
                    FROM Musicas
                    INNER JOIN PlaylistMusicas ON Musicas.Id = PlaylistMusicas.MusicaId
                    WHERE PlaylistMusicas.PlaylistId = @playlistId
                    ORDER BY PlaylistMusicas.DataAdicionado DESC
                    LIMIT 1";
                cmd.Parameters.AddWithValue("@playlistId", playlistId);

                object? resultado = cmd.ExecuteScalar();
                return resultado?.ToString();
            }
        }

        public void SalvarMusicaCompleta(Music musica, long dataModificacao)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"
                INSERT INTO Musicas (Caminho, Titulo, Artista, Album, Genero, DuracaoSegundos, DataModificacao, Imagem,Gosto)
                VALUES (@caminho, @titulo, @artista, @album, @genero, @duracao, @dataMod, @imagem,@gosto)
                ON CONFLICT(Caminho) DO UPDATE SET
                Titulo = @titulo,
                Artista = @artista,
                Album = @album,
                Genero = @genero,
                DuracaoSegundos = @duracao,
                DataModificacao = @dataMod,
                Imagem = @imagem,
                Gosto = @gosto";

                cmd.Parameters.AddWithValue("@caminho", musica.Caminho);
                cmd.Parameters.AddWithValue("@titulo", musica.Titulo);
                cmd.Parameters.AddWithValue("@artista", musica.Artista);
                cmd.Parameters.AddWithValue("@album", musica.Album);
                cmd.Parameters.AddWithValue("@genero", string.IsNullOrEmpty(musica.Genero) ? (object)DBNull.Value : musica.Genero);
                cmd.Parameters.AddWithValue("@duracao", (int)musica.Duracao.TotalSeconds);
                cmd.Parameters.AddWithValue("@dataMod", dataModificacao);
                cmd.Parameters.AddWithValue("@imagem", musica.Image.Length > 0 ? musica.Image : DBNull.Value);
                cmd.Parameters.AddWithValue("@gosto", musica.Gosto ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

        public (Music? Musica, long DataModificacao) ObterMusicaCache(string caminho)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"SELECT Titulo, Artista, Album, Genero, DuracaoSegundos, DataModificacao, Imagem, Gosto
                                    FROM Musicas WHERE Caminho = @caminho";
                cmd.Parameters.AddWithValue("@caminho", caminho);

                using (SqliteDataReader reader = cmd.ExecuteReader()) {
                    if (reader.Read()) {
                        int duracaoSegundos = LerInt32(reader, 4);
                        long dataMod = LerInt64(reader, 5);
                        byte[] imagem = reader.IsDBNull(6)
                            ? Array.Empty<byte>()
                            : reader.GetFieldValue<byte[]>(6);

                        Music musica = new()
                        {
                            Caminho = caminho,
                            Titulo = LerTexto(reader, 0),
                            Artista = LerTexto(reader, 1),
                            Album = LerTexto(reader, 2),
                            Genero = LerTexto(reader, 3),
                            Duracao = TimeSpan.FromSeconds(duracaoSegundos),
                            DuracaoArredondada = TimeSpan.FromSeconds(duracaoSegundos),
                            Image = imagem,
                            Gosto = LerInt32(reader, 7) == 1
                        };

                        return (musica, dataMod);
                    }
                }
            }
            return (null, 0);
        }

        public Dictionary<string, (Music Musica, long DataModificacao)> ObterTodoCache()
        {
            var resultado = new Dictionary<string, (Music, long)>();
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"SELECT Caminho, Titulo, Artista, Album, Genero, DuracaoSegundos,
                          DataModificacao, VezesTocada, Imagem, Gosto
                          FROM Musicas";
                using (SqliteDataReader reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        string caminho = LerTexto(reader, 0);
                        int duracaoSegundos = LerInt32(reader, 5);
                        long dataMod = LerInt64(reader, 6);
                        byte[] imagem = reader.IsDBNull(8)
                            ? Array.Empty<byte>()
                            : reader.GetFieldValue<byte[]>(8);

                        Music musica = new()
                        {
                            Caminho = caminho,
                            Titulo = LerTexto(reader, 1),
                            Artista = LerTexto(reader, 2),
                            Album = LerTexto(reader, 3),
                            Genero = LerTexto(reader, 4),
                            Duracao = TimeSpan.FromSeconds(duracaoSegundos),
                            DuracaoArredondada = TimeSpan.FromSeconds(duracaoSegundos),
                            VezesTocada = LerInt32(reader, 7),
                            Image = imagem,
                            Gosto = LerInt32(reader, 9) == 1
                        };

                        resultado[caminho] = (musica, dataMod);
                    }
                }
            }
            return resultado;
        }

        public void AlternarGosto(string caminho, bool gosto)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE Musicas SET Gosto = @gosto, DataGosto = @data WHERE Caminho = @caminho";
                cmd.Parameters.AddWithValue("@gosto", gosto ? 1 : 0);
                cmd.Parameters.AddWithValue("@data", FormatarData(DateTime.Now));
                cmd.Parameters.AddWithValue("@caminho", caminho);
                cmd.ExecuteNonQuery();
            }
        }

        public void GarantirPlaylistFavoritos()
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"
            INSERT INTO Playlists (Nome, DataCriacao)
            SELECT @nome, @data
            WHERE NOT EXISTS (SELECT 1 FROM Playlists WHERE Nome = @nome)";
                cmd.Parameters.AddWithValue("@nome", "Favoritos");
                cmd.Parameters.AddWithValue("@data", DateTime.Now.ToString());
                cmd.ExecuteNonQuery();
            }
        }

        public List<string> ListarMusicasFavoritas()
        {
            List<string> caminhos = new List<string>();
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Caminho FROM Musicas WHERE Gosto = 1 ORDER BY DataGosto DESC, Id DESC";
                using (SqliteDataReader reader = cmd.ExecuteReader()) {
                    while (reader.Read()) caminhos.Add(reader.GetString(0));
                }
            }
            return caminhos;
        }

        public void SalvarMusicasEmLote(List<(Music Musica, long DataModificacao)> musicas)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                using (var transaction = connection.BeginTransaction()) {
                    SqliteCommand cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                INSERT INTO Musicas (Caminho, Titulo, Artista, Album, Genero, DuracaoSegundos, DataModificacao, Imagem, Gosto)
                VALUES (@caminho, @titulo, @artista, @album, @genero, @duracao, @dataMod, @imagem, @gosto)
                ON CONFLICT(Caminho) DO UPDATE SET
                    Titulo = @titulo, Artista = @artista, Album = @album, Genero = @genero,
                    DuracaoSegundos = @duracao, DataModificacao = @dataMod, Imagem = @imagem, Gosto = @gosto";

                    var pCaminho = cmd.Parameters.Add("@caminho", SqliteType.Text);
                    var pTitulo = cmd.Parameters.Add("@titulo", SqliteType.Text);
                    var pArtista = cmd.Parameters.Add("@artista", SqliteType.Text);
                    var pAlbum = cmd.Parameters.Add("@album", SqliteType.Text);
                    var pGenero = cmd.Parameters.Add("@genero", SqliteType.Text);
                    var pDuracao = cmd.Parameters.Add("@duracao", SqliteType.Integer);
                    var pDataMod = cmd.Parameters.Add("@dataMod", SqliteType.Integer);
                    var pImagem = cmd.Parameters.Add("@imagem", SqliteType.Blob);
                    var pGosto = cmd.Parameters.Add("@gosto", SqliteType.Integer);

                    foreach (var (musica, dataMod) in musicas) {
                        pCaminho.Value = musica.Caminho;
                        pTitulo.Value = musica.Titulo;
                        pArtista.Value = musica.Artista;
                        pAlbum.Value = musica.Album;
                        pGenero.Value = string.IsNullOrEmpty(musica.Genero) ? (object)DBNull.Value : musica.Genero;
                        pDuracao.Value = (int)musica.Duracao.TotalSeconds;
                        pDataMod.Value = dataMod;
                        pImagem.Value = musica.Image.Length > 0 ? musica.Image : (object)DBNull.Value;
                        pGosto.Value = musica.Gosto ? 1 : 0;
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        public void AtualizarMetadados(string caminho, string titulo, string artista, string album, string genero)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();

                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"UPDATE Musicas SET Titulo = @titulo, Artista = @artista, Album = @album, Genero = @genero WHERE Caminho = @caminho";

                cmd.Parameters.AddWithValue("@titulo", titulo);
                cmd.Parameters.AddWithValue("@artista", artista);
                cmd.Parameters.AddWithValue("@album", album);
                cmd.Parameters.AddWithValue("@genero", genero);
                cmd.Parameters.AddWithValue("@caminho", caminho);

                cmd.ExecuteNonQuery();
            }
        }

        public void AtualizarPlaylist(int id, string nome)
        {
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                SqliteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"UPDATE Playlists SET Nome = @nome WHERE Id = @id";
                cmd.Parameters.AddWithValue("@nome", nome);
                cmd.Parameters.AddWithValue("@id", id);

                cmd.ExecuteNonQuery();
            }
        }

        public void GuardarFila(IEnumerable<string> caminhos)
        {
            ArgumentNullException.ThrowIfNull(caminhos);

            List<string> listaCaminhos = caminhos.ToList();
            if (listaCaminhos.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("A fila contém um caminho de música vazio.", nameof(caminhos));

            using SqliteConnection connection = new(_database);
            connection.Open();

            using SqliteTransaction transacao = connection.BeginTransaction();

            List<long> idsMusicas = new();

            using (SqliteCommand localizar = connection.CreateCommand()) {
                localizar.Transaction = transacao;
                localizar.CommandText = "SELECT Id FROM Musicas WHERE Caminho = @caminho";
                SqliteParameter pCaminho = localizar.Parameters.Add("@caminho", SqliteType.Text);

                foreach (string caminho in listaCaminhos) {
                    pCaminho.Value = caminho;
                    object? resultado = localizar.ExecuteScalar();

                    if (resultado == null || resultado == DBNull.Value) {
                        throw new InvalidOperationException(
                            $"A música não está cadastrada no banco e não pode ser salva na fila: {caminho}");
                    }

                    idsMusicas.Add(Convert.ToInt64(resultado));
                }
            }

            using (SqliteCommand apagar = connection.CreateCommand()) {
                apagar.Transaction = transacao;
                apagar.CommandText = "DELETE FROM Fila";
                apagar.ExecuteNonQuery();
            }

            using (SqliteCommand inserir = connection.CreateCommand()) {
                inserir.Transaction = transacao;
                inserir.CommandText = "INSERT INTO Fila (Posicao, MusicaId) VALUES (@posicao, @musicaId)";

                SqliteParameter pPosicao = inserir.Parameters.Add("@posicao", SqliteType.Integer);
                SqliteParameter pMusicaId = inserir.Parameters.Add("@musicaId", SqliteType.Integer);

                for (int i = 0; i < idsMusicas.Count; i++) {
                    pPosicao.Value = i;
                    pMusicaId.Value = idsMusicas[i];
                    inserir.ExecuteNonQuery();
                }
            }

            transacao.Commit();
        }

        public List<string> CarregarFila()
        {
            List<string> caminhos = new();
            using (SqliteConnection connection = new(_database)) {
                connection.Open();
                using (SqliteCommand cmd = connection.CreateCommand()) {
                    cmd.CommandText = @"
                SELECT m.Caminho
                FROM Fila f
                JOIN Musicas m ON m.Id = f.MusicaId
                ORDER BY f.Posicao";
                    using (SqliteDataReader reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            caminhos.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return caminhos;
        }
    }
}