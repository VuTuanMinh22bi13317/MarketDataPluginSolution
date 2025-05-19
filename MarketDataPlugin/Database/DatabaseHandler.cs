// using Microsoft.Data.SqlClient;
// using System;
// using System.Threading.Tasks;

// namespace MarketDataPlugin.Database
// {
//     public class DatabaseHandler
//     {
//         private readonly string _connectionString = "Server=tcp:DESKTOP-6HL7FFG;Database=MarketData;Integrated Security=True;TrustServerCertificate=True;";

//         public async Task InitializeDatabaseAsync()
//         {
//             try
//             {
//                 using var connection = new SqlConnection(_connectionString);
//                 await connection.OpenAsync();

//                 var createTablesQuery = @"
//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_PX')
//                     CREATE TABLE INF_PX (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         MD_ENTRY_TYPE VARCHAR(10),
//                         MD_ENTRY_PX DECIMAL(18,4),
//                         MD_ENTRY_SIZE BIGINT,
//                         PRIMARY KEY (MARKET_ID, SYMBOL, MD_ENTRY_TYPE)
//                     );
//                     IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_INF_PX_SYMBOL' AND object_id = OBJECT_ID('INF_PX'))
//                     CREATE INDEX IX_INF_PX_SYMBOL ON INF_PX(SYMBOL);

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_PX_RECOVERY')
//                     CREATE TABLE INF_PX_RECOVERY (
//                         MARKET_ID VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         TRADING_SESSION_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         OPEN_PX DECIMAL(15, 4),
//                         HIGH_PX DECIMAL(15, 4),
//                         LOW_PX DECIMAL(15, 4),
//                         SYMBOL_CLOSE_INFO_PX DECIMAL(13,6),
//                         OPN_PX_YLD FLOAT(53),
//                         HIGH_PX_YLD FLOAT(53),
//                         LOW_PX_YLD FLOAT(53),
//                         CLS_PX_YLD FLOAT(53),
//                         TOTAL_VOLUME_TRADED INT,
//                         GROSS_TRADE_AMT FLOAT(53),
//                         SELL_TOT_ORDER_QTY BIGINT,
//                         BUY_TOT_ORDER_QTY BIGINT,
//                         SELL_VALID_ORDER_CNT BIGINT,
//                         BUY_VALID_ORDER_CNT BIGINT,
//                         NO_MD_ENTRIES BIGINT,
//                         RPT_SEQ VARCHAR(3),
//                         MD_UPDATE_ACTION VARCHAR(3),
//                         MD_ENTRY_TYPE VARCHAR(10),
//                         MD_ENTRY_POSITION_NO INT,
//                         MD_ENTRY_PX DECIMAL(18,4),
//                         MD_ENTRY_SIZE BIGINT,
//                         NUMBER_OF_ORDERS BIGINT,
//                         MD_ENTRY_YIELD FLOAT(53),
//                         MD_ENTRY_MM_SIZE BIGINT,
//                         PRIMARY KEY (MARKET_ID, SYMBOL, MD_ENTRY_TYPE)
//                     );
//                     IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_INF_PX_RECOVERY_SYMBOL' AND object_id = OBJECT_ID('INF_PX_RECOVERY'))
//                     CREATE INDEX IX_INF_PX_RECOVERY_SYMBOL ON INF_PX_RECOVERY(SYMBOL);

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_SECURITY_DEFINITION')
//                     CREATE TABLE INF_SECURITY_DEFINITION (
//                         MARKET_ID VARCHAR(50),
//                         TOT_NUM_REPORTS BIGINT,
//                         SECURITY_EXCHANGE VARCHAR(10),
//                         SYMBOL VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         TICKER_CODE VARCHAR (50),
//                         SYMBOL_SHORT_CODE VARCHAR(10),
//                         SYMBOL_NAME VARCHAR(200),
//                         SYMBOL_ENGLISH_NAME VARCHAR(100),
//                         PRODUCT_ID VARCHAR(20),
//                         PRODUCT_GRP_ID VARCHAR(10),
//                         SECURITY_GROUP_ID VARCHAR(10),
//                         PUT_OR_CALL VARCHAR(10),
//                         EXERCISE_STYLE VARCHAR(10),
//                         MATURITY_MONTH_YEAR VARCHAR(10),
//                         MATURITY_DATE VARCHAR(20),
//                         ISSUER VARCHAR(10),
//                         ISSUE_DATE VARCHAR(20),
//                         CONTRACT_MULTIPLIER FLOAT(53),
//                         COUPON_RATE FLOAT(53),
//                         CURRENCY VARCHAR(10),
//                         HIGH_LIMIT_PX DECIMAL(15,4),
//                         LOW_LIMIT_PX DECIMAL(15,4),
//                         STRIKE_PX DECIMAL(18,8),
//                         SECURITY_STATUS VARCHAR(10),
//                         CONTRACT_SIZE FLOAT(53),
//                         REF_PX DECIMAL(15,4),
//                         PREV_CLOSE_PX DECIMAL(15,4),
//                         SYMBOL_CLOSE_INFO_PX_TYPE VARCHAR(10),
//                         RANDOM_END_CODE VARCHAR(10),
//                         VWAP FLOAT(53),
//                         PRIMARY KEY (MARKET_ID, SYMBOL)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_SECURITY_STATUS')
//                     CREATE TABLE INF_SECURITY_STATUS (
//                         MARKET_ID VARCHAR(50),
//                         TSC_PROD_GRP_ID VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         BOARD_EVT_ID VARCHAR(50),
//                         PRIMARY KEY (MARKET_ID, TSC_PROD_GRP_ID, BOARD_ID)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_SECURITY_INFO_NOTIFICATION')
//                     CREATE TABLE INF_SECURITY_INFO_NOTIFICATION (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, BOARD_ID)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_SYMBOL_CLOSING_INFORMATION')
//                     CREATE TABLE INF_SYMBOL_CLOSING_INFORMATION (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         SYMBOL_CLOSE_INFO_PX DECIMAL(18,4),
//                         SYMBOL_CLOSE_INFO_YIELD DECIMAL(18,4),
//                         SYMBL_CLOSE_INFO_PX_TYPE VARCHAR(10),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, BOARD_ID)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_VOLATILITY_INTERRUPTION')
//                     CREATE TABLE INF_VOLATILITY_INTERRUPTION (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         VI_TYPE_CODE VARCHAR(10),
//                         VI_KIND_CODE VARCHAR(10),
//                         STATIC_VI_BASE_PX DECIMAL(18,4),
//                         DYNAMIC_VI_BASE_PRICE DECIMAL(18,4),
//                         VI_PX DECIMAL(18,4),
//                         STATIC_VI_DISPARTIY_RATIO DECIMAL(18,4),
//                         DYNAMIC_VI_DISPARTIY_RATIO DECIMAL(18,4),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, BOARD_ID)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_MARKET_MAKER_INFORMATION')
//                     CREATE TABLE INF_MARKET_MAKER_INFORMATION (
//                         MARKET_ID VARCHAR(50),
//                         MARKET_MAKER_CONTRACT_CODE VARCHAR(50),
//                         MEMBER_NO VARCHAR(50),
//                         PRIMARY KEY (MARKET_ID, MARKET_MAKER_CONTRACT_CODE, MEMBER_NO)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_SYMBOL_EVENT')
//                     CREATE TABLE INF_SYMBOL_EVENT (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         EVENT_KIND_CODE VARCHAR(10),
//                         EVENT_START_DATE DATE,
//                         EVENT_OCCURRENCE_REASON_CODE VARCHAR(50),
//                         EVENT_END_DATE DATE,
//                         PRIMARY KEY (MARKET_ID, SYMBOL, EVENT_KIND_CODE, EVENT_START_DATE)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_INDEX_CONSTITUENTS')
//                     CREATE TABLE INF_INDEX_CONSTITUENTS (
//                         MARKET_ID VARCHAR(50),
//                         MARKET_INDEX_CLASS VARCHAR(10),
//                         INDEXS_TYPE_CODE VARCHAR(50),
//                         PRIMARY KEY (MARKET_ID, MARKET_INDEX_CLASS, INDEXS_TYPE_CODE)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_RANDOM_END')
//                     CREATE TABLE INF_RANDOM_END ( 
//                         MARKET_ID VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         TRANSACT_TIME DATETIME2,
//                         RAND_END_APPLY_CLASS VARCHAR(50),
//                         RAND_END_EXEC_PX DECIMAL(18,4),
//                         RAND_END_HIGHEST_PX DECIMAL(18,4),
//                         RAND_END_HIGHEST_PX_RATIO DECIMAL(18,6),
//                         RAND_END_LOWEST_PX DECIMAL(18,4),
//                         RAND_END_LOWEST_PX_RATIO DECIMAL(18,6),
//                         LATEST_PX DECIMAL(18,4),
//                         LATEST_PX_RATIO DECIMAL(18,6),
//                         RAND_END_RELEASE_TIME DATETIME,
//                         PRIMARY KEY (MARKET_ID, BOARD_ID, SYMBOL, TRANSACT_TIME)
//                     );


//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_INDEX')
//                     CREATE TABLE INF_INDEX (
//                         MARKET_ID VARCHAR(50),
//                         INDEXS_TYPE_CODE VARCHAR(50),
//                         TRADING_SESSION_ID VARCHAR(50),
//                         TRANSACT_TIME DATETIME2,
//                         VALUE_INDEXES DECIMAL(18,2),
//                         TOTAL_VOLUME_TRADED BIGINT,
//                         GROSS_TRADE_AMT DECIMAL(18,4),
//                         CONTAUCT_ACC_TRDVOL BIGINT,
//                         CONTAUCT_ACC_TRDVAL DECIMAL(18,4),
//                         BLKTRD_ACC_TRDVOL BIGINT,
//                         BLKTRD_ACC_TRDVAL DECIMAL(18,4),
//                         FLU_UPPER_LIMIT_COUNT INT,
//                         FLU_UP_COUNT INT,
//                         FLU_STEADINESS_COUNT INT,
//                         FLU_DOWN_COUNT INT,
//                         FLU_LOWER_LIMIT_COUNT INT,
//                         FLU_UP_VOLUME BIGINT,
//                         FLU_DOWN_VOLUME BIGINT,
//                         FLU_STEADINESS_VOLUME BIGINT,
//                         PRIMARY KEY (MARKET_ID, INDEXS_TYPE_CODE, TRADING_SESSION_ID, TRANSACT_TIME)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_INV_IND')
//                     CREATE TABLE INF_INV_IND (
//                         MARKET_ID VARCHAR(50),
//                         MARKET_INDEX_CLASS VARCHAR(10),
//                         INDEXS_TYPE_CODE VARCHAR(50),
//                         TRANSACT_TIME DATETIME2,
//                         INVEST_CODE VARCHAR(50),
//                         SELL_VOLUME BIGINT,
//                         SELL_TRADE_AMOUNT DECIMAL(18,4),
//                         BUY_VOLUME BIGINT,
//                         BUY_TRADED_AMOUNT DECIMAL(18,4),
//                         PRIMARY KEY (MARKET_ID, MARKET_INDEX_CLASS, INDEXS_TYPE_CODE, TRANSACT_TIME, INVEST_CODE)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_INV_SYMBOL')
//                     CREATE TABLE INF_INV_SYMBOL (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         INVEST_CODE VARCHAR(50),
//                         SELL_VOLUME BIGINT,
//                         SELL_TRADE_AMOUNT DECIMAL(18,4),
//                         BUY_VOLUME BIGINT,
//                         BUY_TRADED_AMOUNT DECIMAL(18,4),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, INVEST_CODE)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_TOP_N_MEMBERS_SYMBOL')
//                     CREATE TABLE INF_TOP_N_MEMBERS_SYMBOL (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         SELL_RANK_SEQ INT,
//                         BUY_RANK_SEQ INT,
//                         SELL_MEMBER_NO VARCHAR(50),
//                         SELL_VOLUME BIGINT,
//                         SELL_TRADE_AMOUNT DECIMAL(18,4),
//                         BUY_MEMBER_NO VARCHAR(50),
//                         BUY_VOLUME BIGINT,
//                         BUY_TRADED_AMOUNT DECIMAL(18,4),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, SELL_RANK_SEQ, BUY_RANK_SEQ)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_OPEN_INTEREST')
//                     CREATE TABLE INF_OPEN_INTEREST (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         TRADE_DATE DATE,
//                         OPEN_INTEREST_QTY BIGINT,
//                         PRIMARY KEY (MARKET_ID, SYMBOL, TRADE_DATE)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_DEEM_TRADE_PX')
//                     CREATE TABLE INF_DEEM_TRADE_PX (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         EXPECTED_TRADE_PX DECIMAL(18,4),
//                         EXPECTED_TRADE_QTY BIGINT,
//                         PRIMARY KEY (MARKET_ID, SYMBOL, BOARD_ID)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_FOREIGNER_ORDER_LIMIT')
//                     CREATE TABLE INF_FOREIGNER_ORDER_LIMIT (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         FOREIGNER_BUY_POSBL_QTY BIGINT,
//                         FOREIGNER_ORDER_LIMIT_QTY BIGINT,
//                         PRIMARY KEY (MARKET_ID, SYMBOL)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_PRICELIMIT_EXPANSION')
//                     CREATE TABLE INF_PRICELIMIT_EXPANSION (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         TRANSACT_TIME DATETIME2,
//                         HIGH_LIMIT_PRICE DECIMAL(18,4),
//                         LOW_LIMIT_PRICE DECIMAL(18,4),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, BOARD_ID, TRANSACT_TIME)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_ETF_INAV')
//                     CREATE TABLE INF_ETF_INAV (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         TRANSACT_TIME DATETIME2,
//                         INAV_VALUE DECIMAL(18,4),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, TRANSACT_TIME)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_ETFI_INDEX')
//                     CREATE TABLE INF_ETFI_INDEX (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         TRANSACT_TIME DATETIME2,
//                         VALUE_INDEXES DECIMAL(18,2),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, TRANSACT_TIME)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_ETF_TRACKING_ERROR')
//                     CREATE TABLE INF_ETF_TRACKING_ERROR (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         TRADE_DATE DATE,
//                         TRACKING_ERROR DECIMAL(18,6),
//                         DISPARATE_RATIO DECIMAL(18,6),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, TRADE_DATE)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_TOP_SYMBOLS_QTY')
//                     CREATE TABLE INF_TOP_SYMBOLS_QTY (
//                         MARKET_ID VARCHAR(50),
//                         TOT_NUM_REPORTS BIGINT,
//                         SYMBOL VARCHAR(50),
//                         RANK INT,
//                         MD_ENTRY_SIZE BIGINT,
//                         PRIMARY KEY (MARKET_ID, SYMBOL, RANK)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_TOP_SYMBOLS_PX')
//                     CREATE TABLE INF_TOP_SYMBOLS_PX (
//                         MARKET_ID VARCHAR(50),
//                         TOT_NUM_REPORTS BIGINT,
//                         SYMBOL VARCHAR(50),
//                         RANK INT,
//                         MD_ENTRY_PX DECIMAL(18,4),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, RANK)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_TOP_SYMBOLS_HIGH_PX')
//                     CREATE TABLE INF_TOP_SYMBOLS_HIGH_PX (
//                         MARKET_ID VARCHAR(50),
//                         TOT_NUM_REPORTS BIGINT,
//                         SYMBOL VARCHAR(50),
//                         RANK INT,
//                         PX_FLUCTUATION_RATIO DECIMAL(18,2),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, RANK)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_TOP_SYMBOLS_LOW_PX')
//                     CREATE TABLE INF_TOP_SYMBOLS_LOW_PX (
//                         MARKET_ID VARCHAR(50),
//                         TOT_NUM_REPORTS BIGINT,
//                         SYMBOL VARCHAR(50),
//                         RANK INT,
//                         PX_FLUCTUATION_RATIO DECIMAL(18,2),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, RANK)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_FOREIGN_RESULT')
//                     CREATE TABLE INF_FOREIGN_RESULT (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         BOARD_ID VARCHAR(50),
//                         TRADING_SESSION_ID VARCHAR(50),
//                         TRANSACT_TIME DATETIME,
//                         FORN_INVEST_TYPE_CODE VARCHAR(10),
//                         SELL_VOLUME BIGINT,
//                         SELL_TRADE_AMOUNT DECIMAL(18,4),
//                         BUY_VOLUME BIGINT,
//                         BUY_TRADED_AMOUNT DECIMAL(18,4),
//                         TOTAL_SELL_VOLUME BIGINT,
//                         TOTAL_SELL_TRADE_AMOUNT DECIMAL(18,4),
//                         TOTAL_BUY_VOLUME BIGINT,
//                         TOTAL_BUY_TRADED_AMOUNT DECIMAL(18,4),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, BOARD_ID, TRADING_SESSION_ID, TRANSACT_TIME)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_DISCLOSURE')
//                     CREATE TABLE INF_DISCLOSURE (
//                         MARKET_ID VARCHAR(50),
//                         SYMBOL VARCHAR(50),
//                         DISCLOSURE_ID VARCHAR(50),
//                         CURRENT_MSG_NO INT,
//                         SECURITY_EXCHANGE VARCHAR(10),
//                         TOTAL_MSG_NO INT,
//                         LANQUAGE_CATEGORY VARCHAR(10),
//                         DATA_CATEGORY VARCHAR(10),
//                         PUBLIC_INFORMATION_DATE DATE,
//                         TRANSMISSION_DATE DATE,
//                         PROCESS_TYPE VARCHAR(10),
//                         HEADLINE VARCHAR(250),
//                         BODY VARCHAR(1000),
//                         PRIMARY KEY (MARKET_ID, SYMBOL, DISCLOSURE_ID, CURRENT_MSG_NO)
//                     );

//                     IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'INF_DRV_PRODUCT_EVENT')
//                     CREATE TABLE INF_DRV_PRODUCT_EVENT (
//                         PRODUCT_ID VARCHAR(50),
//                         EVENT_KIND_CODE VARCHAR(10),
//                         EVENT_START_DATE DATE,
//                         EVENT_OCCURRENCE_REASON_CODE VARCHAR(50),
//                         EVENT_END_DATE DATE,
//                         PRIMARY KEY (PRODUCT_ID, EVENT_KIND_CODE, EVENT_START_DATE)
//                     );";

//                 using var command = new SqlCommand(createTablesQuery, connection);
//                 await command.ExecuteNonQueryAsync();
//                 Console.WriteLine("Database initialized successfully.");
//             }
//             catch (Exception ex)
//             {
//                 throw new Exception("Failed to initialize database: " + ex.Message);
//             }
//         }
//     }
// }