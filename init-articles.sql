CREATE TABLE IF NOT EXISTS articles (
    id SERIAL PRIMARY KEY,
    title TEXT NOT NULL,
    body TEXT NOT NULL,
    publishedutc TIMESTAMP NOT NULL
);

-- 25 artikler spredt over de sidste 20 dager
INSERT INTO articles (title, body, publishedutc)
SELECT
  'Article #' || g AS title,
  'This is the content of article #' || g AS body,
  (NOW() AT TIME ZONE 'utc') - ((g % 20) || ' days')::INTERVAL
FROM generate_series(1,25) g;
