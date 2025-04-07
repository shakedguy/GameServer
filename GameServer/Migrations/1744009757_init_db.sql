
CREATE TABLE IF NOT EXISTS players(
       Id TEXT PRIMARY KEY,
       DeviceId TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_players_on_id_and_device_id ON players (Id, DeviceId);

CREATE TABLE IF NOT EXISTS player_states(
     PlayerId TEXT PRIMARY KEY,
     Coins INTEGER DEFAULT 0,
     Rolls INTEGER DEFAULT 0,
     FOREIGN KEY (PlayerId) REFERENCES players(Id) ON DELETE CASCADE
);