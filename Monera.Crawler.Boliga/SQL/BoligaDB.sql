USE [BoligaDB]
GO
/****** Object:  Table [dbo].[BoligaProperty]    Script Date: 11.04.2016 17:36:28 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BoligaProperty]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[BoligaProperty](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[SogeresultaterGuid] [uniqueidentifier] NOT NULL,
	[Link] [nvarchar](max) NULL,
	[Titel] [nvarchar](max) NULL,
	[Postnr] [char](4) NULL,
	[PostnrTitel] [nvarchar](max) NULL,
	[Kontantpris] [money] NULL,
	[Ejerudgift] [money] NULL,
	[Kvmpris] [money] NULL,
	[Type] [nvarchar](max) NULL,
	[Bolig] [int] NULL,
	[Grund] [int] NULL,
	[Vaerelser] [int] NULL,
	[Etage] [nvarchar](max) NULL,
	[Byggear] [int] NULL,
	[Oprettet] [date] NULL,
	[Liggetid] [int] NULL,
	[BrokerLink] [nvarchar](max) NULL,
	[ButikTitel] [nvarchar](max) NULL,
	[ButikAdresse] [nvarchar](max) NULL,
	[ButikPostnr] [char](4) NULL,
	[ButikPostnrTitel] [nvarchar](max) NULL,
	[PrisforskelProcentdel] [int] NULL,
	[KvmprisBoligen] [money] NULL,
	[KvmprisOmradet] [money] NULL,
 CONSTRAINT [PK_BoligaProperty] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
SET ANSI_PADDING OFF
GO
