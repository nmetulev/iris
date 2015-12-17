USE [pragirissqloutputstreamdb]
GO

/****** Object: Table [dbo].[pragirissqloutputstreamtable] Script Date: 12/16/2015 8:58:17 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

Drop Table [dbo].[pragirissqloutputstreamtable] 
Go


CREATE TABLE [dbo].[pragirissqloutputstreamtable] (
    [Id]                    INT            IDENTITY (1, 1) NOT NULL,
    [DoorID]				Int,
    [Success]               NVARCHAR (MAX) NULL,
    [Probability]           DECIMAL Null,
    [Anger]                 DECIMAL Null,
    [Contempt]              DECIMAL Null,
    [Disgust]               DECIMAL Null,
    [Fear]                  DECIMAL Null,
    [Happiness]             DECIMAL Null,
    [Neutral]               DECIMAL Null,
    [Sadness]               DECIMAL Null,
    [Surprise]              DECIMAL Null,
    [Temperature]           DECIMAL Null,
    [Noise]                 DECIMAL Null,
    [Brightness]            DECIMAL Null,
    [Humidity]              DECIMAL Null
    
);

GO
CREATE CLUSTERED INDEX myIndex2 ON [pragirissqloutputstreamtable] (Id)
GO


Select * from [pragirissqloutputstreamtable] order by id desc