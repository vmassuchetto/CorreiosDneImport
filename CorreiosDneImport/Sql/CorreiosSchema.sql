DROP TABLE IF EXISTS CorreiosPais;
DROP TABLE IF EXISTS CorreiosFaixaUf;
DROP TABLE IF EXISTS CorreiosFaixaUop;
DROP TABLE IF EXISTS CorreiosUnidOper;
DROP TABLE IF EXISTS CorreiosGrandeUsuario;
DROP TABLE IF EXISTS CorreiosVarLog;
DROP TABLE IF EXISTS CorreiosNumSec;
DROP TABLE IF EXISTS CorreiosLogradouro;
DROP TABLE IF EXISTS CorreiosFaixaBairro;
DROP TABLE IF EXISTS CorreiosVarBai;
DROP TABLE IF EXISTS CorreiosBairro;
DROP TABLE IF EXISTS CorreiosFaixaCpc;
DROP TABLE IF EXISTS CorreiosCpc;
DROP TABLE IF EXISTS CorreiosVarLoc;
DROP TABLE IF EXISTS CorreiosFaixaLocalidade;
DROP TABLE IF EXISTS CorreiosLocalidade;

CREATE TABLE CorreiosLocalidade (
  LocNu int NOT NULL,
  UfeSg char(2) NOT NULL DEFAULT '',
  LocNo varchar(72) NOT NULL DEFAULT '',
  Cep varchar(8) DEFAULT NULL,
  LocInSit char(1) NOT NULL DEFAULT '',
  LocInTipoLoc char(1) NOT NULL DEFAULT '',
  LocNuSub int DEFAULT NULL,
  LocNoAbrev varchar(36) DEFAULT NULL,
  MunNu varchar(7) DEFAULT NULL,
  CONSTRAINT PK_CorreiosLocalidade
	PRIMARY KEY (LocNu)
);

CREATE TABLE CorreiosFaixaLocalidade (
  LocNu int NOT NULL,
  LocCepIni varchar(8) NOT NULL DEFAULT '',
  LocCepFim varchar(8) NOT NULL DEFAULT '',
  LocTipoFaixa varchar(1) NOT NULL DEFAULT '',
  CONSTRAINT PK_CorreiosFaixaLocalidade
	PRIMARY KEY (LocNu, LocCepIni, LocTipoFaixa),
  CONSTRAINT FK_CorreiosFaixaLocalidade_CorreiosLocalidade
	FOREIGN KEY (LocNu) REFERENCES CorreiosLocalidade(LocNu)
);

CREATE TABLE CorreiosVarLoc (
  LocNu int NOT NULL,
  ValNu int NOT NULL,
  ValTx varchar(72) NOT NULL DEFAULT '',
  CONSTRAINT PK_CorreiosVarLoc
	PRIMARY KEY (LocNu, ValNu),
  CONSTRAINT FK_CorreiosVarLoc_CorreiosLocalidade
	FOREIGN KEY (LocNu) REFERENCES CorreiosLocalidade(LocNu)
);

CREATE TABLE CorreiosCpc (
  CpcNu int NOT NULL,
  UfeSg varchar(2) NOT NULL DEFAULT '',
  LocNu int NOT NULL,
  CpcNo varchar(72) NOT NULL DEFAULT '',
  CpcEndereco varchar(100) NOT NULL DEFAULT '',
  Cep varchar(8) NOT NULL DEFAULT '',
  CONSTRAINT PK_CorreiosCpc
	PRIMARY KEY (CpcNu),
  CONSTRAINT FK_CorreiosCpc_CorreiosLocalidade
	FOREIGN KEY (LocNu) REFERENCES CorreiosLocalidade(LocNu)
);

CREATE TABLE CorreiosFaixaCpc (
  CpcNu int NOT NULL,
  CpcInicial varchar(6) NOT NULL DEFAULT '',
  CpcFinal varchar(6) NOT NULL DEFAULT '',
  CONSTRAINT PK_CorreiosFaixaCpc
	PRIMARY KEY (CpcNu, CpcInicial),
  CONSTRAINT FK_CorreiosFaixaCpc_CorreiosCpc
	FOREIGN KEY (CpcNu) REFERENCES CorreiosCpc(CpcNu)
);

CREATE TABLE CorreiosBairro (
  BaiNu int NOT NULL,
  UfeSg varchar(2) NOT NULL DEFAULT '',
  LocNu int NOT NULL DEFAULT '',
  BaiNo varchar(72) NOT NULL DEFAULT '',
  BaiNoAbrev varchar(36) DEFAULT '',
  CONSTRAINT PK_BaiNu
	PRIMARY KEY (BaiNu),
  CONSTRAINT FK_CorreiosBairro_CorreiosLocalidade
	FOREIGN KEY (LocNu) REFERENCES CorreiosLocalidade(LocNu)
);

CREATE TABLE CorreiosVarBai (
  BaiNu int NOT NULL,
  VdbNu int NOT NULL,
  VdbTx varchar(72) NOT NULL DEFAULT '',
  CONSTRAINT PK_CorreiosVarBai
	PRIMARY KEY (BaiNu, VdbNu),
  CONSTRAINT FK_CorreiosVarBai_CorreiosBairro
	FOREIGN KEY (BaiNu) REFERENCES CorreiosBairro(BaiNu)
);

CREATE TABLE CorreiosFaixaBairro (
  BaiNu int NOT NULL,
  FcbCepIni varchar(8) NOT NULL DEFAULT '',
  FcbCepFim varchar(8) NOT NULL DEFAULT '',
  CONSTRAINT PK_CorreiosFaixaBairro
	PRIMARY KEY (BaiNu, FcbCepIni),
  CONSTRAINT FK_CorreiosFaixaBairro_CorreiosBairro
	FOREIGN KEY (BaiNu) REFERENCES CorreiosBairro(BaiNu)
);

CREATE TABLE CorreiosLogradouro (
  LogNu int NOT NULL,
  UfeSg varchar(2) NOT NULL DEFAULT '',
  LocNu int NOT NULL,
  BaiNuIni int NOT NULL,
  BaiNuFim int DEFAULT NULL,
  LogNo varchar(100) NOT NULL DEFAULT '',
  LogComplemento varchar(100) DEFAULT NULL,
  Cep varchar(8) NOT NULL DEFAULT '',
  TloTx varchar(36) NOT NULL DEFAULT '',
  LogStaTlo varchar(1) NOT NULL DEFAULT '',
  LogNoAbrev varchar(36) DEFAULT NULL,
  CONSTRAINT PK_CorreiosLogradouro
	PRIMARY KEY (LogNu),
  CONSTRAINT FK_CorreiosLogradouro_CorreiosLocalidade
	FOREIGN KEY (LocNu) REFERENCES CorreiosLocalidade(Locnu),
  CONSTRAINT FK_CorreiosLogradouro_CorreiosBairroIni
	FOREIGN KEY (BaiNuIni) REFERENCES CorreiosBairro(BaiNu),
  CONSTRAINT FK_CorreiosLogradouro_CorreiosBairroFim
	FOREIGN KEY (BaiNuFim) REFERENCES CorreiosBairro(BaiNu)
);

CREATE TABLE CorreiosNumSec (
  LogNu int NOT NULL,
  SecNuIni varchar(10) NOT NULL DEFAULT '',
  SecNuFim varchar(10) NOT NULL DEFAULT '',
  SecInLado varchar(1) NOT NULL DEFAULT '',
  CONSTRAINT PK_CorreiosNumSec
	PRIMARY KEY (LogNu),
  CONSTRAINT FK_CorreiosNumSec_CorreiosLogradouro
	FOREIGN KEY (LogNu) REFERENCES CorreiosLogradouro(LogNu)
);

CREATE TABLE CorreiosVarLog (
  LogNu int NOT NULL,
  VloNu int NOT NULL,
  TloTx varchar(36) NOT NULL DEFAULT '',
  VloTx varchar(150) NOT NULL DEFAULT '',
  CONSTRAINT PK_CorreiosVarLog
	PRIMARY KEY (LogNu, VloNu),
  CONSTRAINT FK_CorreiosVarLog_CorreiosLogradouro
	FOREIGN KEY (LogNu) REFERENCES CorreiosLogradouro(LogNu)
);

CREATE TABLE CorreiosGrandeUsuario (
  GruNu int NOT NULL,
  UfeSg varchar(2) NOT NULL DEFAULT '',
  LocNu int NOT NULL,
  BaiNu int NOT NULL,
  LogNu int DEFAULT NULL,
  GruNo varchar(72) NOT NULL DEFAULT '',
  GruEndereco varchar(100) NOT NULL DEFAULT '',
  Cep varchar(8) NOT NULL DEFAULT '',
  GruNoAbrev varchar(36) DEFAULT '',
  CONSTRAINT PK_CorreiosGrandeUsuario
	PRIMARY KEY (GruNu),
  CONSTRAINT FK_CorreiosGrandeUsuario_CorreiosLocalidade
	FOREIGN KEY (LocNu) REFERENCES CorreiosLocalidade(LocNu),
  CONSTRAINT FK_CorreiosGrandeUsuario_CorreiosLogradouro
	FOREIGN KEY (LogNu) REFERENCES CorreiosLogradouro(LogNu),
  CONSTRAINT FK_CorreiosGrandeUsuario_CorreiosBairro
	FOREIGN KEY (BaiNu) REFERENCES CorreiosBairro(BaiNu)
);

CREATE TABLE CorreiosUnidOper (
  UopNu int NOT NULL,
  UfeSg varchar(2) NOT NULL DEFAULT '',
  LocNu int NOT NULL,
  BaiNu int NOT NULL,
  LogNu int DEFAULT NULL,
  UopNo varchar(100) NOT NULL DEFAULT '',
  UopEndereco varchar(100) NOT NULL DEFAULT '',
  Cep varchar(8) NOT NULL DEFAULT '',
  UopInCp char(1) NOT NULL DEFAULT '',
  UopNoAbrev varchar(36) DEFAULT '',
  CONSTRAINT PK_CorreiosUnidOper
	PRIMARY KEY (UopNu),
  CONSTRAINT FK_CorreiosUnidOper_CorreiosLocalidade
	FOREIGN KEY (LocNu) REFERENCES CorreiosLocalidade(LocNu),
  CONSTRAINT FK_CorreiosUnidOper_CorreiosLogradouro
	FOREIGN KEY (LogNu) REFERENCES CorreiosLogradouro(LogNu)
);

CREATE TABLE CorreiosFaixaUop (
  UopNu int NOT NULL,
  FncInicial int NOT NULL,
  FncFinal int NOT NULL,
  CONSTRAINT PK_CorreiosFaixaUop
	PRIMARY KEY (UopNu, FncInicial),
  CONSTRAINT FK_CorreiosFaixaUop_CorreiosUnidOper
	FOREIGN KEY (UopNu) REFERENCES CorreiosUnidOper(UopNu),
);

CREATE TABLE CorreiosFaixaUf (
  UfeSg char(2) NOT NULL DEFAULT '',
  UfeCepIni char(8) NOT NULL DEFAULT '',
  UfeCepFim char(8) NOT NULL DEFAULT '',
  CONSTRAINT PK_CorreiosFaixaUf
	PRIMARY KEY (UfeSg, UfeCepIni)
);

CREATE TABLE CorreiosPais (
  Sg varchar(2) NOT NULL,
  SgAlternativa varchar(3) NOT NULL DEFAULT '',
  NoPortugues varchar(72) NOT NULL DEFAULT '',
  NoIngles varchar(72) DEFAULT '',
  NoFrances varchar(72) DEFAULT '',
  Abreviatura varchar(36) DEFAULT '',
  CONSTRAINT PK_CorreiosPais
	PRIMARY KEY (Sg)
);