CREATE TABLE IF NOT EXISTS comments (
    id SERIAL PRIMARY KEY,
    articleid INT NOT NULL,
    author TEXT NOT NULL,
    content TEXT NOT NULL,
    createdutc TIMESTAMP NOT NULL
);

-- 50 kommentarer fordelt på 25 artikler
INSERT INTO comments (articleid, author, content, createdutc)
SELECT
  (g % 25) + 1 AS articleid,
  'User_' || (g % 10) AS author,
  'This is comment #' || g || ' for article #' || ((g % 25) + 1),
  (NOW() AT TIME ZONE 'utc') - ((g % 5) || ' hours')::INTERVAL
FROM generate_series(1,50) g;
