Jeg har bygget to services:

- Aeticleservice → styrer artiklerne og gemmer de nyeste i en cache
- CommentService → styrer kommentarer og bruger en cache når det giver mening

Begge services har hver deres database (PostgreSQL) og hver deres cache (Redis).  
