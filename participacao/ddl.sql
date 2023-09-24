-- Data Definition Language is used to create and modify the 
-- structure of objects in a database using predefined commands
-- and a specific syntax. 
CREATE TABLE public.pessoas (
	id UUID PRIMARY KEY NOT NULL,
	apelido VARCHAR(32) UNIQUE NOT NULL,
	nome VARCHAR(100) NOT NULL,
	nascimento DATE NOT NULL,
	stack TEXT NULL
);