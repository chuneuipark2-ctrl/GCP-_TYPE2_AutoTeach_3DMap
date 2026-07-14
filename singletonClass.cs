using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace gcp_Wpf
{
    public struct Gcp_Info
    {
        
        // define fields and properties for the shared struct here
        public int srmCount { get; set; }
        public int language { get; set; }
        public string buildDate { get; set; }
        public bool isAdminMode { get; set; }
    }


    public struct Srm_Info
    {
        // define fields and properties for the shared struct here

        // Srm Info
        public int srmID { get; set; }
        public int srmType { get; set; }
        public int forkCnt { get; set; }
        public int forkType { get; set; }

        public int forkLeftLimit { get; set; }
        public int forkRightLimit { get; set; }

        // 이동 방향 설정 (0: 정방향, 1: 반대방향)
        public int travDirectionReverse { get; set; }

        // Rack Info
        public int row { get; set; }
        public int bay { get; set; }
        public int lev { get; set; }
        public int stn { get; set; }

        public int[] cellBay { get; set; }
        public int[] cellLev { get; set; }

        public int prohCnt { get; set; }

        public string[] prohRack { get; set; }

        public int[][] prohDataList;
        public int prohParseCnt { get; set; }
        //public fixed int cellBay[256];
        //public fixed int cellLev[128];

        public int offsetUp { get; set; }
        public int offsetDown { get; set; }

        // Station Info
        public byte stnCount { get; set; } 
        public Srm_Station[] SrmStation { get; set; }

        // Comm Info
        public string srmIP { get; set; }
        public int srmPORT { get; set; }

        // Host Info
        public int hostPORT { get; set; }
        public uint hostTimeout { get; set; }
        public int heartBeatCheck { get; set; }
        public int heartBeatTimeout { get; set; }
        public int modemErrorCheck { get; set; }

        // Dio Info
        public int dioType { get; set; }
        public int dioUse { get; set; }
        public int sfUse { get; set; }
        // Dio Modbus Rtu
        public string comPORT { get; set; }
        public int baudRate { get; set; }
        public int parity { get; set; }
        public int dataBit { get; set; }
        public int stopBit { get; set; }
        // Dio Fastech (UDP)
        public string dioIP { get; set; }      // 로터리 스위치 셀렉션 ID에 따라 IP 설정 됨
        public int dioID { get; set; }      // DIO 보드ID - Default 0

        // Select State         - UI 내 사용선택 정보 구동 시 에만 저장 - 저장안함
        public bool bUse_fork1 { get; set; }
        public bool bUse_fork2 { get; set; }

        // DIO Alive Cnt
        public long dioAliveCnt { get; set; }

        // CV 인터록 무시
        public bool ignoreCV { get; set; }
        // 공출고/공입고 무시
        public bool ignoreGoods { get; set; }

        public bool pollingStop { get; set; }

        public bool initialdSt1StartSt {  get; set; }
    }

    public struct Srm_Station
    {
        public byte stnType;            // 1: 입고, 2: 출고, 3: 입출고, 4: 가상스테이션                 
        public byte goodType;           // 0: 모든화물 비허용
        public int travPos;             // 주행 mm
        public int liftPos;             // 승강 mm
        public short forkPos;           // 포크 mm
        public byte upOffset;           // 상위치 오프셋
        public byte downOffset;         // 하위치 오프셋
        public byte intNum;             // 인터록 센서 번호
    }

    public struct Gcp_State         // 지상반 호기별 상태정보 저장
    {
        public byte heartBeat;        // 지상반 heartBeat
        public byte gcpRxMode;        // 지상반 모드 Rx 값
        public byte gcpTxMode;        // 지상반 모드 Tx 값
        public bool safetyPlug;     // 
        public bool faultReset;     // DI 리셋버튼 입력  or 프로그램 리셋버튼 클릭
        public bool emStop;         // B접점으로 DI접점은 1이 정상 ---- 통신 송신은  안눌림(정상):0  눌림(비상):1 으로 처리
    }
    unsafe public struct Srm_State         // 크레인 호기별 상태정보 저장
    {
        public string protocolVer;        // 프로토콜 버전
        //public fixed byte firmwareVer[4];        // 펌웨어 버전  10 = Ver1.0
        public string firmwareVer;        // 펌웨어 버전  10 = Ver1.0
        //public byte buildYear;          // 펌웨어 빌드연도
        //public byte buildMonth;         // 펌웨어 빌드 월
        //public byte buildDay;           // 펌웨어 빌드 일
        public uint utcTime;            // UTC Time
        //public fixed byte projNo[6];    // 프로젝트 번호
        public string projNo;    // 프로젝트 번호
        public byte groupNo;            // 그룹 번호
        public ushort srmNo;            // 호기 번호
        public ushort srmType;          // 장비타입

        // 지상반송신 상태 송/수신 정보확인용
        public Gcp_State gcpState;

        public fixed byte CVOK[8];      // 통신 인터록  (CV->장비)  스테이션 당 1bit사용  8*8 64개 스테이션 사용
        public fixed byte CVNO[8];      // 통신 인터록  (장비->CV)  

        //public byte devMode;            // 비트 분류
        public byte setupMode;            // Bit3(8): 셋업모드 ----- OFF:0, ON:1
        public byte forcedMode;            // Bit2(4): 강제모드 ----- OFF:0, ON:1
        public byte manualMode;            // Bit1(2): 수동모드 ----- OFF:0, ON:1
        public byte autoMode;            // Bit0(1): 자동모드 ----- OFF:0, ON:1

        //public byte devState1;          // 비트 분류
        public byte dSt1ReqCmd;         // Bit5 작업요구 비트
        public byte dSt1InvConn;        // Bit4 인버터 접속상태
        public byte dSt1Abnormal;       // Bit3 이상 상태       
        public byte dSt1Warning;        // Bit2 경고 상태 
        public byte dSt1EmStop;         // Bit1 비상 정지
        public byte dSt1StartSt;        // Bit0 시작 상태

        //public byte devState2;          // 비트 분류
        public byte dSt2EmSwitch;       // Bit7 비상스위치 상태       
        public byte dSt2ManAutoSw;      // Bit6 자동/수동 스위치 상태 
        public byte dSt2maintPos;       // Bit1 보수 위치
        public byte dSt2homePos;        // Bit0 홈 위치

        public byte operCode;           // 장비 동작코드
        public byte errcodeH;           // 이상코드 대분류
        public byte errcodeM;           // 이상코드 중분류
        public ushort errcodeL;           // 이상코드 소분류

        public FORK_State fork1;
        public FORK_State fork2;

        public TRAV_State trav;
        public LIFT_State lift;

        // Task 작업정보
        public uint taskNo;
        public byte taskState;

        public fixed byte dInput[16];
        public fixed byte dOutput[5];

        // 인버터 에러코드
        public byte invErrorTravMain;
        public byte invErrorTravSub;

        public byte invErrorLiftMain;
        public byte invErrorLiftSub;

        public byte invErrorFork1Main;
        public byte invErrorFork1Sub;

        public byte invErrorFork2Main;
        public byte invErrorFork2Sub;

        public byte invContInput;
        public byte invContOutput;

        public byte invLiftInput;
        public byte invLiftOutput;

        public byte invTrav1Input;
        public byte invTrav1Output;

        public byte invTrav2Input;
        public byte invTrav2Output;

        public byte invForkInput;
        public byte invForkOutput;

        public Extension_State extState;
    }

    public struct Extension_State
    {
        public int travSetSpd;
        public int liftSetSpd;
        public int fork1SetSpd;
        public int fork2SetSpd;
        public int travSetAcc;
        public int liftSetAcc;
        public int fork1SetAcc;
        public int fork2SetAcc;
        public int travSetDec;
        public int liftSetDec;
        public int fork1SetDec;
        public int fork2SetDec;
        public int travSetJerk;
        public int liftSetJerk;
        public int fork1SetJerk;
        public int fork2SetJerk;
        public short preLoadMoveDelay;
        public short postLoadMoveDelay;
        public short preLoadForkExtendDelay;
        public short postLoadForkExtendDelay;
        public short preLoadForkLiftDelay;
        public short postLoadForkLiftDelay;
        public short preLoadForkRetractDelay;
        public short postLoadForkRetractDelay;
        public short preUnloadMoveDelay;
        public short postUnloadMoveDelay;
        public short preUnloadForkExtendDelay;
        public short postUnloadForkExtendDelay;
        public short preUnloadForkLiftDelay;
        public short postUnloadForkLiftDelay;
        public short preUnloadForkRetractDelay;
        public short postUnloadForkRetractDelay;
        public int travMotorTorque;
        public int liftMotorTorque;
        public int fork1MotorTorque;
        public int fork2MotorTorque;
        public int totalOperationTime;
        public int travOperationTime;
        public int liftOperationTime;
        public int fork1OperationTime;
        public int fork2OperationTime;
        public int travBrakeOpenCount;
        public int liftBrakeOpenCount;
        public int fork1BrakeOpenCount;
        public int fork2BrakeOpenCount;
    }
    public struct SRM_IO
    {
        // DI 0
        public bool EM;
        public bool AUTO;
        public bool MAN;
        public bool RDF;
        public bool LST;
        public bool TST;
        public bool MFLT;
        public bool GOV;

        // DO 0
        public bool IINH;
        public bool FCD;
        public bool RDE;
        public bool RED;
        public bool YEL;
        public bool GRN;
        public bool SUD;
        public bool MCE;

        // DI 1
        public bool MCF;
        public bool MC1F;
        public bool PDR;
        public bool PTH;
        public bool MCTMF;
        public bool MCFMF;
        public bool T1PSF;
        public bool T1OSO;

        // DO 1
        public bool MCUB;
        public bool PLAMP;
        public bool PFAN;
        public bool MCTM;
        public bool MCFM1;
        public bool T1FSPC;
        public bool T1SPO;
        public bool MCFB1;

        // DI 2
        public bool LBMMSF1;
        public bool TBMMSF1;
        public bool FBMMSF1;
        public bool CPTF;
        public bool TDF;
        public bool TDR;
        public bool THP;
        public bool TSP;

        // DO 2
        public bool COSE;       // 
        public bool CENB;
        public bool CRST;
        public bool MCLM;
        public bool MCFM2;
        public bool LFSPC;
        public bool LSPO;
        public bool MCFB2;

        // DI 3
        public bool CFLT;
        public bool CRD;
        public bool MC2F;
        public bool MCLMF;
        public bool MCFM2F;
        public bool L1PSF;
        public bool L1OSO;
        public bool FBMMSF2;

        // DO 3
        public bool CVNO1;
        public bool CVNO2;
        public bool CVNO3;
        public bool CVNO4;
        public bool CVNO5;
        public bool CVNO6;
        public bool CVNO7;
        public bool CVNO8;

        // DI 4
        public bool CVOK1;
        public bool CVOK2;
        public bool CVOK3;
        public bool CVOK4;
        public bool CVOK5;
        public bool CVOK6;
        public bool CVOK7;
        public bool CVOK8;

        // DO 4
        public bool GRA_RST;
        public bool DEVICE_RST;
        public bool LED_RD;
        public bool LED_GR;
        public bool LED_BU;

        // DI 5
        public bool GRA;
        public bool DEVICE_FLT;
        public bool TS1_ENB;
        public bool TS2_ENB;
        public bool M_EST;
        public bool M_KEYSW;
        public bool M_FLT;
        public bool M_DOORSW;

        // DI 6
        public bool LBMMSF2;
        public bool TBMMSF2;
        public bool F1ENC;
        public bool LDU;
        public bool LDD;
        public bool LHP;
        public bool LSP;
        public bool GOX1;
        public bool GOXH1;

        // DI 7
        public bool GOXM1;
        public bool GOXS1;
        public bool GWL1;
        public bool GWR1;
        public bool GWLe1;
        public bool GWRe1;
        public bool GDFL1;
        public bool GDFR1;

        // DI 8
        public bool GDRL1;
        public bool GDRR1;
        public bool GHL1;
        public bool GHR1;
        public bool FOKL1;
        public bool FOKR1;
        public bool FEL1;
        public bool FER1;

        // DI 9
        public bool FCL1;
        public bool FCR1;
        public bool DSTL1;
        public bool DSTR1;
        public bool DSTLe1;
        public bool DSTRe1;
        public bool RTF;
        public bool RTR;

        // DI 10
        public bool RTF2;
        public bool RTR2;
        public bool GOX2;
        public bool GOXH2;
        public bool GOXM2;
        public bool GOXS2;
        public bool GWL2;
        public bool GWR2;

        // DI 11
        public bool GWLe2;
        public bool GWRe2;
        public bool GDFL2;
        public bool GDFR2;
        public bool GDRL2;
        public bool GDRR2;
        public bool GHL2;
        public bool GHR2;

        // DI 12
        public bool FOKL2;
        public bool FOKR2;
        public bool FEL2;
        public bool FER2;
        public bool FCL2;
        public bool FCR2;
        public bool DSTL2;
        public bool DSTR2;

        // DI 13
        public bool DSTLe2;
        public bool DSTRe2;
        public bool ODSTL1;
        public bool ODSTR1;
        public bool DSTLR1;
        public bool DSTRR1;
        public bool ODSTL2;
        public bool ODSTR2;

        // DI 14
        public bool DSTLR2;
        public bool DSTRR2;
        public bool FML1;
        public bool FMR1;
        public bool FHL1;
        public bool FHR1;
        public bool FML2;
        public bool FMR2;

        // DI 15
        public bool FHL2;
        public bool FHR2;

        // DI Addition [260714 PCE Lidar signal 추가]
        public bool LiDAR1_Observe_Signal;
        public bool LiDAR1_Alert_Signal;
        public bool LiDAR1_Alarm_Signal;
        public bool LiDAR1_System_Alarm;
        public bool LiDAR2_Observe_Signal;
        public bool LiDAR2_Alert_Signal;
        public bool LiDAR2_Alarm_Signal;
        public bool LiDAR2_System_Alarm;



    }
    
    public struct FORK_State
    {
        public byte curStation;           // 포크 스테이션
        public ushort curBay;
        public byte curLev;
        public sbyte curPosNum;             //  F정 = (-3) = M정 = (-2) = H정 = (-1) = C정 = (1) = H정 = (2) = M정 = (3) = F정      - 포크 중간 중간 포지션 위치 값 표시  Signed
       // public byte curPos1;                //  Bit7-2
        public byte posRightBottom;        // Bit5 포크1 우 승강하위치
        public byte posRightUp;       // Bit4 포크1 우 승강상위치    
        public byte posRightTravExac;       // Bit3 포크1 우 주행정위치
        public byte posLeftBottom;        // Bit2 포크1 좌 승강하위치
        public byte posLeftUp;        // Bit1 포크1 좌 승강상위치
        public byte posLeftTravExac;       // Bit0 포크1 좌 주행정위치

        // public byte curPos2;                // 
        public byte posRightExac3;        // Bit6 포크 우 정위치3
        public byte posRightExac2;        // Bit5 포크 우 정위치2   
        public byte posRightExac1;        // Bit4 포크 우 정위치1
        public byte posLeftExac3;         // Bit3 포크 좌 정위치3
        public byte posLeftExac2;         // Bit2 포크 좌 정위치2
        public byte posLeftExac1;         // Bit1 포크 좌 정위치1
        public byte posCenterExac;        // Bit0 포크 센터정위치

        public byte targetStation;          // 포크 목적 스테이션
        public byte targetRow;
        public byte targetLev;
        public ushort targetBay;

        //public byte state1;                 // 
        public byte forkRightEnable; // Bit7 포크 우 진출가능   0: 진출불가, 1: 진출가능
        public byte forkLeftEnable;  // Bit6 포크 좌 진출가능
        public byte loadState;       // Bit5 화물적재
        public byte originPos;       // Bit4 정위치    
        public byte moveDirec;       // Bit3 이동방향
        public byte decState;        // Bit2 감속상태
        public byte accState;        // Bit1 가속상태
        public byte operState;       // Bit0 동작상태

        //public byte state2;                 // 
        public byte loadTunn;        // Bit4 부하 튜닝중
        public byte noLoadTunn;      // Bit3 무부하 튜닝중
        public byte homeCheck;       // Bit2 원점확인
        public byte invAlarmSt;      // Bit1 인버터 알람상태
        public byte invConnSt;       // Bit0 인버터 접속상태

        public byte loadType;               // 적재 화물타입
        public int curPos;
        public short curSpd;
        public int targetPos;
        public short targetSpd;

        // 작업정보 - 반송명령 
        public uint jobNo;
        public byte taskIdx;
        // From     5Byte
        public byte fromStation;
        public byte fromRow;
        public ushort fromBay;
        public byte fromLev;
        // To       5Byte
        public byte toStation;
        public byte toRow;
        public ushort toBay;
        public byte toLev;

        public byte cmdCode;
        public byte procState;
        public byte procStep;

        // 작업정보 - 이동작업
        public uint mvJobNo;
        public byte mvToStation;
        public byte mvToRow;
        public ushort mvToBay;
        public byte mvToLev;

        public byte mvProcState;
        public byte mvProcStep;

    }

    public struct TRAV_State
    {
        //public byte state1;                 //
        public byte homeMove;        // Bit5 홈복귀
        public byte trSt1OriginPos;       // Bit4 정위치    
        public byte trSt1MoveDirec;       // Bit3 이동방향
        public byte trSt1DecState;        // Bit2 감속상태
        public byte trSt1AccState;        // Bit1 가속상태
        public byte trSt1OperState;       // Bit0 동작상태

        //public byte state2;                 // 
        public byte trSt2LoadTunn;        // Bit4 부하 튜닝중
        public byte trSt2NoLoadTunn;      // Bit3 무부하 튜닝중
        public byte trSt2HomeCheck;       // Bit2 원점확인
        public byte trSt2InvAlarmSt;      // Bit1 인버터 알람상태
        public byte trSt2InvConnSt;       // Bit0 인버터 접속상태

        public byte fwDecNo;
        public byte bwDecNo;

        public int curPos;
        public short curSpd;
        public int targetPos;
        public short targetSpd;
    }

    public struct LIFT_State
    {
        //public byte state1;                 // 
        public byte homeMove;        // Bit5 홈복귀
        public byte liSt1OriginPos;       // Bit4 정위치    
        public byte liSt1MoveDirec;       // Bit3 이동방향
        public byte liSt1DecState;        // Bit2 감속상태
        public byte liSt1AccState;        // Bit1 가속상태
        public byte liSt1OperState;       // Bit0 동작상태

        //public byte state2;                 // 
        public byte liSt2LoadTunn;        // Bit4 부하 튜닝중
        public byte liSt2NoLoadTunn;      // Bit3 무부하 튜닝중
        public byte liSt2HomeCheck;       // Bit2 원점확인
        public byte liSt2InvAlarmSt;      // Bit1 인버터 알람상태
        public byte liSt2InvConnSt;       // Bit0 인버터 접속상태

        public byte upDecNo;
        public byte dnDecNo;

        public int curPos;
        public short curSpd;
        public int targetPos;
        public short targetSpd;
    }

    public struct Str_RecvHeader
    {
        public byte srcType;
        public byte srcID;
        public byte dstType;
        public byte dstID;
        public byte seqNum;
        public byte byPass1;
        public byte byPass2;
        public byte cmd1;
        public ushort len; //DATA 길이 + 1 (CMD2 길이)
        public byte cmd2;
        public byte[] data;
    }

    public struct Str_SendHeader
    {
        public byte srcType;
        public byte srcID;
        public byte dstType;
        public byte dstID;
        public byte seqNum;
        public byte byPass1;
        public byte byPass2;
        public byte cmd1;
        public ushort len; //DATA 길이 + 1 (CMD2 길이)
        public byte cmd2;
        public byte[] data;
    }

    enum PULSECMD
    {
        NONE,
        START,
        HOMERTN,
        RESET,
        DELETE,
        STOP,
        EMSTOP
    }

    public enum JOBSTATE
    {
        // WCS -> GCP
        NONE,       // 작업 없음
        WAIT,       // 작업 대기  WCS 데이터 없을 때,
        RECEIVE,    // 작업 수신  WCS 데이터 썼을 때,
        DATAOK,     // DATA OK 대기
        SEND,       // 작업 전송  
        // GCP -> SRM
        PEND,       // 작업 전송완료
        EXEC,       // 작업 실행 (동작 중)
        COMPLETE,   // 작업 완료
        CLEARJOB,   // WCS 작업삭제 대기
        STOP,       // 작업 중 정지 상태 (에러)
        EMSTOP
    }

    // 제어 로직 스텝 공용 사용 변수 정의------------------------------------------------------------------------
    public struct Srm_Packet
    {
        public byte curStation;           // 포크 스테이션
        public ushort curBay;
        public byte curLev;
        //--------------------------------------------------------------------------------

        public bool oldAbnormal;        // 이상 상태 변동 저장 플래그
        public byte oldErrCode;            // 에러코드 변경 체크 플래그
        public byte oldSubCode;

        public bool gcpError;           // 지상반 에러상태
        public byte gcpErrorCode;       // 지상반 에러코드
        public byte gcpSubCode;         // 지상반 서브코드
        public bool offlineErrorLogged; // OFFLINE 전환요청 로그 1회 출력 플래그
        public byte srmResCode;         // 기상반 명령응답코드

        public bool gcpWarning;         // 지상반 경고상태
        public byte gcpWarningCode;     // 지상반 경고코드
        public byte gcpWarningSubCode;  // 지상반 경고서브코드
        public byte oldWarningCode;     // 경고코드 변경 체크 플래그
        public byte oldWarningSubCode;

        public bool gcpModemFlt;     // 지상반 광모뎀 신호 카운트
        public int gcpModemFltCnt;     // 지상반 광모뎀 신호 카운트

        public bool heartBeatError;     // WCS HeartBeat Error
        public byte lastHeartBeat;      // Last WCS HeartBeat Value
        public DateTime lastHeartBeatTime; // Last WCS HeartBeat Change Time

        public bool jobError;           // 작업 송신에러

        public bool recovError;         // 회복가능 에러 비트

        public int resMainCode;      // 회복가능 에러 서브코드
        public int resSubCode;      // 회복가능 에러 서브코드

        public int resStr;          // 작업분석결과 문자열 저장버퍼

        public string recvJobString;    // 수신작업 명
        //--------------------------------------------------------------------------------
        public bool manClicked;         //  수동버튼 클릭여부 확인

        public byte manPosStd;          // 좌우 정위치 기준        1: 포크1좌 2: 포크1우 3: 포크2좌 4:포크2우

        public bool manStop;         //  수동버튼 해제여부 확인
        public int manCmd;
        public int manAxis;
        public byte manTrav;
        public byte manLift;
        public byte manFork1;
        public byte manFork2;

        public byte manForkMvType;      // 수동 포크 조작 타입

        //--------------------------------지상반 조작-------------------------------------
        public bool buzzerStop;

        public bool pulseClicked;         //    단일동작 명령 클릭여부 확인 (지상반 버튼조작)
        // 0x0050   시작 ON/OFF
        public byte startCmd;
        public byte startOnOff;           //    0x0050 시작명령 플래그 0: OFF, 1: ON
        public bool startEnable;          //    시작 가능상태 체크
        // 0x0051   홈복귀
        public byte homeCmd;
        // 0x0052   이상리셋
        public byte resetCmd;
        // 0x0094   랙 설정 조회
        public bool rackRequest;
        public int rackReqType;
        public int rackReqCount;

        // 0x0098   스테이션 설정 조회
        public bool stationRequest;
        // 0x009C   금지렉 설정 조회
        public bool prohRackRequest;
        // 0x0110   기본정보 조회
        public bool stdInfoRequest;
        // 0x0111   기본정보 제어
        public bool stdInfoControl;
        // 0x0125   장치 구조 조회
        public bool craneSetRequest;
        // 0x00A7   포크데이터 설정 조회
        public bool forkRequest;

        // 0x0058   모드설정
        public bool modeSetReq;              // 모드 설정 리퀘스트 플래그
        public byte modeSetCmd;              // 0: 수동모드, 1: 셋업모드, 2: 자동모드
        public byte modeSetOpt;              // 1: 강제모드

        // 0x0095   셀 위치 설정 (Write)
        public bool cellPosWriteReq;
        public int cellPosWriteType;         // 1: Bay, 2: Level
        public int cellPosWriteStart;
        public int cellPosWriteEnd;
        public int[] cellPosWriteData;       // 쓸 위치값 배열
        public bool cellPosWriteDone;        // 완료 플래그
        public bool cellPosWriteNack;        // 실패 플래그
        public byte cellPosWriteNackReason;

        // 0x0059   프로토콜 탐색 (Probe)
        public bool probeReq;                // 전송 요청
        public byte[] probeData;             // 전송할 데이터
        public bool probeDone;               // 응답 수신 완료
        public byte[] probeResp;             // 수신된 응답 전체
        public int probeRespLen;             // 응답 길이

        // 0xA3/A4  Drive 파라미터 (주행)
        public bool driveParamReadReq;       // 0xA3 읽기 요청
        public bool driveParamReadDone;      // 읽기 완료
        public byte[] driveParamData;        // 수신된 ParamRes (1018B)
        public bool driveParamWriteReq;      // 0xA4 쓰기 요청
        public byte[] driveParamWriteData;   // 전송할 ParamCTRL (1053B)
        public bool driveParamWriteDone;     // 쓰기 응답 수신
        public byte driveParamWriteResult;   // 0=ACK

        // 0xA5/A6  Lift 파라미터 (승강)
        public bool liftParamReadReq;
        public bool liftParamReadDone;
        public byte[] liftParamData;         // 수신된 ParamRes (1018B)
        public bool liftParamWriteReq;
        public byte[] liftParamWriteData;    // 전송할 ParamCTRL (1053B)
        public bool liftParamWriteDone;
        public byte liftParamWriteResult;

        // 0x0059   보수위치 이동
        public bool maintMoveReq;            // 이동 요청
        public bool maintMoveDone;           // 응답 수신
        public byte maintMoveResult;         // 0=성공

        // 0x0053   작업삭제
        public byte flagJobDelete;          // 0x0053 작업삭제 플래그

        public bool autoJobDelete;          // 자동 작업삭제 (Vexi 테스트작업 Clear 용)

        public byte manuJobDelete;          // 수동 작업 삭제 버튼클릭
        public byte manuJobComplete;        // 수동 작업 완료 버튼클릭

        public byte manuFork1JobDelete;
        public byte manuFork2JobDelete;
        public byte manuFork1JobComplete;
        public byte manuFork2JobComplete;

        public bool semiJobClicked;         //    반자동 명령 클릭여부 확인 (지상반 버튼조작)

        //--------------------------------상위명령 조작요청 및 응답-------------------------------------
        public bool wcsJobReceive;          //    WCS 작업명령 수신확인
        public bool wcsJobReceiveFlag;      //    WCS 작업명령 수신확인 모니터링 용

        // D7025 단일작업명령 -> D7625 ACK
        public bool wcsCmdHomeReturn;     //    WCS -> GCP 펄스명령 - 홈복귀
        public bool wcsCmdReset;          //    WCS -> GCP 펄스명령 - 이상리셋
        public bool wcsCmdDeleteAll;      //    WCS -> GCP 펄스명령 - 작업삭제
        public bool wcsCmdDeleteFork1;    //    WCS -> GCP 펄스명령 - 포크1 작업삭제
        public bool wcsCmdDeleteFork2;    //    WCS -> GCP 펄스명령 - 포크2 작업삭제
        public bool wcsCmdSrmOnline;      //    WCS -> GCP 펄스명령 - SRM Online
        public bool wcsCmdSrmManual;      //    WCS -> GCP 펄스명령 - SRM Manual  --> 온라인 해제
        public bool wcsCmdCycleStop;      //    WCS -> GCP 펄스명령 - Cycle Stop --> 동작 완료 후 정지
        public bool wcsCmdEmergencyStop;  //    WCS -> GCP 펄스명령 - Emergency Stop --> 긴급 정지

        // D7630:0~1
        public byte wcsReqCompleteFork1;  //    GCP -> WCS 펄스명령 - 포크1 작업완료
        public byte wcsReqCompleteFork2;  //    GCP -> WCS 펄스명령 - 포크2 작업완료
        // D7630:4~5
        public byte wcsReqDeleteFork1;    //    GCP -> WCS 펄스명령 - 포크1 작업삭제
        public byte wcsReqDeleteFork2;    //    GCP -> WCS 펄스명령 - 포크2 작업삭제

        // D7625 ACK 변수 선언
        public bool wcsAckHomeReturn;     //    GCP -> WCS 펄스응답 - 홈복귀
        public bool wcsAckReset;          //    GCP -> WCS 펄스응답 - 이상리셋
        public bool wcsAckDeleteAll;      //    GCP -> WCS 펄스응답 - 작업삭제
        public bool wcsAckDeleteFork1;    //    GCP -> WCS 펄스응답 - 포크1 작업삭제
        public bool wcsAckDeleteFork2;    //    GCP -> WCS 펄스응답 - 포크2 작업삭제
        public bool wcsAckSrmOnline;      //    GCP -> WCS 펄스응답 - SRM Online
        public bool wcsAckSrmManual;      //    GCP -> WCS 펄스응답 - SRM Manual  --> 온라인 해제
        public bool wcsAckCycleStop;      //    GCP -> WCS 펄스응답 - Cycle Stop --> 동작 완료 후 정지
        public bool wcsAckEmergencyStop;  //    GCP -> WCS 펄스응답 - Emergency Stop --> 긴급 정지


        //public byte wcsReqDeleteFork1;    //    GCP -> WCS 펄스명령 - 포크1 작업삭제
        //public byte wcsReqDeleteFork2;    //    GCP -> WCS 펄스명령 - 포크2 작업삭제

        //--------------------------------반송작업 데이터-----------------------------------
        public bool notDeleteTestJob;       // 테스트 반송작업 번호 존재
        public bool notPrecessedJob;        // 미완료 작업 재전송 플래그
        public byte notPrecessedIdx;        // 미완료 작업 인덱스

        public byte reqJobCodeFk1;           //    요청작업 코드 - 이동/입고/출고 등
        public byte reqJobCodeFk2;           //    요청작업 코드 - 이동/입고/출고 등

        public ushort reqWcsCodeFk1;
        public ushort reqWcsCodeFk2;
        // Fork 1
        public uint reqJobNoFk1;          //    포크1 작업번호
        public uint preJobNoFk1;          //    포크1 이전 작업번호

        public byte reqJobStepFk1;        //    포크1 작업스텝
        public byte reqFromStFk1;         //    포크1 From Station
        public byte reqFromRowFk1;        //    포크1 From Row
        public ushort reqFromBayFk1;      //    포크1 From Bay
        public byte reqFromLevFk1;        //    포크1 From Lev

        public byte reqToStFk1;           //    포크1 To Station
        public byte reqToRowFk1;          //    포크1 To Row
        public ushort reqToBayFk1;        //    포크1 To Bay
        public byte reqToLevFk1;          //    포크1 To Lev

        public byte reqGoodsTypeFk1;      //    포크1 Goods Type

        // Fork 2
        public uint reqJobNoFk2;          //    포크2 작업번호
        public uint preJobNoFk2;          //    포크2 이전 작업번호

        public byte reqJobStepFk2;        //    포크2 작업스텝
        public byte reqFromStFk2;         //    포크2 From Station
        public byte reqFromRowFk2;        //    포크2 From Row
        public ushort reqFromBayFk2;      //    포크2 From Bay
        public byte reqFromLevFk2;        //    포크2 From Lev

        public byte reqToStFk2;           //    포크2 To Station
        public byte reqToRowFk2;          //    포크2 To Row
        public ushort reqToBayFk2;        //    포크2 To Bay
        public byte reqToLevFk2;          //    포크2 To Lev

        public byte reqGoodsTypeFk2;      //    포크2 Goods Type


        //---------------------------- 작업처리용 버퍼------------------------------------------
        public byte semiJobCodeFk1;           //    수신작업 코드 - 이동/입고/출고 등
        public byte semiJobCodeFk2;           //    수신작업 코드 - 이동/입고/출고 등

        public byte semiSendCodeFk1;           //    송신작업 코드 - 이동/입고/출고 등
        public byte semiSendCodeFk2;           //    송신작업 코드 - 이동/입고/출고 등

        // Fork 1
        public uint semiJobNoFk1;          //    포크1 작업번호

        public byte semiJobStepFk1;        //    포크1 작업스텝
        public byte semiFromStFk1;         //    포크1 From Station
        public byte semiFromRowFk1;        //    포크1 From Row
        public ushort semiFromBayFk1;      //    포크1 From Bay
        public byte semiFromLevFk1;        //    포크1 From Lev

        public byte semiToStFk1;           //    포크1 To Station
        public byte semiToRowFk1;          //    포크1 To Row
        public ushort semiToBayFk1;        //    포크1 To Bay
        public byte semiToLevFk1;          //    포크1 To Lev

        public byte semiGoodsTypeFk1;      //    포크1 Goods Type

        // Fork 2
        public uint semiJobNoFk2;          //    포크2 작업번호

        public byte semiJobStepFk2;        //    포크2 작업스텝
        public byte semiFromStFk2;         //    포크2 From Station
        public byte semiFromRowFk2;        //    포크2 From Row
        public ushort semiFromBayFk2;      //    포크2 From Bay
        public byte semiFromLevFk2;        //    포크2 From Lev

        public byte semiToStFk2;           //    포크2 To Station
        public byte semiToRowFk2;          //    포크2 To Row
        public ushort semiToBayFk2;        //    포크2 To Bay
        public byte semiToLevFk2;          //    포크2 To Lev

        public byte semiGoodsTypeFk2;      //    포크2 Goods Type

        // Move Dest
        public byte semiDestSt;           //    Dest Station
        public byte semiDestRow;          //    Dest Row
        public ushort semiDestBay;        //    Dest Bay
        public byte semiDestLev;          //    Dest Lev

        //------------------------------------------------------------------------------------------------------------


        public bool deleteFlag;           //    0x0053 작업삭제 플래그 Bit7: Fork1 작업, Bit6: Fork2 작업, Bit5: Task Command 모두 삭제
                                          //    

        public bool operState;              //      동작 상태 갱신
        public bool jobEffectiveChk;        //      WCS 수신작업 유효성 체크

        //-----------------------------to do Variable---------------------------------------------------------------------
        public byte jobRequestOld;      //      SRM 작업요구 비트 ON/OFF체크 버퍼
        public bool jobRequest;         //      GCP 작업요구비트 to WCS
        public int oldJobState;         //      이전작업상태
        public int jobState;            //      작업상태

        public byte fork1JobComplete;   //      Fork1 작업완료비트
        public byte fork2JobComplete;   //      Fork2 작업완료비트

        public int jobParseState;       //      수신작업 파싱상태
        public DateTime completeStateDataReportOKWaitTime;  // COMPLETE 상태에서 dataReportOK==0 대기 시작 시각 (CLEARJOB→COMPLETE 후)
        public bool dataReportOKTimeoutError;               // dataReportOK 대기 타임아웃 에러 표시 여부 (중복 메시지 방지)
        //----------------------------------------------------------------------------------------------------------------------

        // Comm State
        public int srmCommDiscCnt;
        public bool stSrmComm;
        public bool txSrmComm;  // GCP -> SRM 
        public bool rxSrmComm;  // SRM -> GCP
        public DateTime lastUdpReceiveTime;  // 마지막 UDP 수신 시간 (광모뎀 에러 체크용)
        public bool isAutoTeaching;          // 오토티칭 실행 중 플래그 (광모뎀 에러 체크 비활성화용)

        public int dioCommDiscCnt;
        public bool stDioComm;
        public bool txDioComm;  // GCP -> IO (Modbus RTU) 
        public bool rxDioComm;  // IO  -> GCP

        public int wcsCommDiscCnt;
        public bool stWcsComm;
        public bool txWcsComm;  // GCP -> WCS
        public bool rxWcsComm;  // WCS -> GCP


        // Received Packet
        public Str_RecvHeader recvStr;
        public Str_SendHeader sendStr;
    }

    public struct Dio_Setting
    {
        public string name { get; set; }

        public uint pin { get; set; }

        public bool mask { get; set; }

        public bool value { get; set; }

        public bool prevalue { get; set; }
    }
    //-----------------------------------------------------------------------------------------------------------

    unsafe public struct Dio_Packet
    {
        public bool DO_TESTMODE;

        public uint DISTATUS;    // to do - Unsafe 고정버퍼로 할지...
        public uint DOSTATUS;
        public uint DOCOMMAND;
        public bool[] DIBIT;
        public bool[] DOCMD;
        public bool[] DOBIT;
        public Dio_Setting[] DISET;
        public Dio_Setting[] DOSET;
        public Dictionary<int, int> inputReference;     // 저장 된 DINPUT 선택정보
        public Dictionary<int, bool> inputControl;      // DINPUT 상태 값

        // OUTPUT Monitor
        public Dictionary<int, int> outputReference;     // 저장 된 DOUTPUT 선택정보
        public Dictionary<int, bool> outputControl;      // DOUTPUT 상태 값
        
    }

    public struct Wcs_Packet
    {
        public ushort[] WCSTO;
        public ushort[] WCSTOBUF;
        public ushort[] WCSTOEX;
        public ushort[] WCSFROMBUF;
        public ushort[] WCSFROM;
        public Wcs_From WCS_PARSE;
        public Wcs_From WCS_BUF;
    }


    // WCS FROM 데이터 파싱 및 머징

    public struct Wcs_From
    {
        public int reqEqid;                     // 상태요청 장비 ID
        public int reqType;                     // 상태요청 타입

        public byte cmdPriority;                // D7000  tp2 to do 명령 우선순위 수신 및 매핑

        public ushort fork1JobNo;                // D7001

        public byte fork1JobCmd;                 // D7002
        public byte fork1Move;                    // D7002.0
        public byte fork1Storage;                 // D7002.1
        public byte fork1Retrieval;               // D7002.2
        public byte fork1RackToRack;              // D7002.3
        public byte fork1StToSt;                  // D7002.4
        public byte fork1ChangeRack;              // D7002.5
        public byte fork1ChangeSt;                // D7002.6
        public byte fork1Sticky;                  // D7002.7

        public ushort fork1FromSt;              // D7003
        public ushort fork1FromRow;             // D7004
        public ushort fork1FromBay;             // D7005
        public ushort fork1FromLev;             // D7006
        public ushort fork1ToSt;                // D7007
        public ushort fork1ToRow;               // D7008
        public ushort fork1ToBay;               // D7009
        public ushort fork1ToLev;               // D7010

        public ushort fork2JobNo;                // D7011

        public byte fork2JobCmd;                 // D7012
        public byte fork2Move;                    // D7012.0
        public byte fork2Storage;                 // D7012.1
        public byte fork2Retrieval;               // D7012.2
        public byte fork2RackToRack;              // D7012.3
        public byte fork2StToSt;                  // D7012.4
        public byte fork2ChangeRack;              // D7012.5
        public byte fork2ChangeSt;                // D7012.6
        public byte fork2Sticky;                  // D7012.7

        public ushort fork2FromSt;              // D7013
        public ushort fork2FromRow;             // D7014
        public ushort fork2FromBay;             // D7015
        public ushort fork2FromLev;             // D7016

        public ushort fork2ToSt;                // D7017
        public ushort fork2ToRow;               // D7018
        public ushort fork2ToBay;               // D7019
        public ushort fork2ToLev;               // D7020



        public byte heartBeat;                  // D7025.0
        public byte homeReturn;                 // D7025.1
        public byte errorReset;                 // D7025.2
        public byte jobDelete;                  // D7025.3
        public byte fork1Delete;                // D7025.4
        public byte fork2Delete;                // D7025.5
        public byte timeSynchro;                // D7025.6
        public byte dataReportOK;               // D7025.8
        public byte srmOnline;                  // D7025.A
        public byte srmManual;                  // D7025.B
        public byte srmCycleStop;               // D7025.E      to do 방법확인 필요
        public byte srmEmStop;                  // D7025.F

        // D7031~37
        public ushort timeYear;
        public ushort timeMonth;
        public ushort timeDate;
        public ushort timeHour;
        public ushort timeMinute;
        public ushort timeSecond;
        public ushort timeDay;
    }

    public struct SharedStruct
    {
        // define fields and properties for the shared struct here
        public int test1;
        public int test2;
        public Gcp_Info GcpInfo;
        public Srm_Info[] SrmInfo;
        public Dio_Packet[] DioPacket;
        public Srm_Packet[] SrmPacket;
        public Srm_State[] SrmState;
        public Wcs_Packet[] WcsPacket;
        public SRM_IO[] SRMIO;
        public int SharedField { get; set; }
    }

    public class singletonClass
    {
        private static readonly Lazy<singletonClass> lazyInstance = new Lazy<singletonClass>(() => new singletonClass());
        public static singletonClass Instance { get { return lazyInstance.Value; } }
        public int test;
        private SharedStruct SharedData;

        

        private singletonClass()
        {
            /* 통합 지상반 SRM 3개 호기 까지 연결 가능
             * 공용 화면의 경우 gClass.srmNum(현재 선택 된 호기를 저장하는 글로벌 변수) 을 인덱스로 사용
             * 개별 화면의 경우 각 클래스 인덱스 번호를 가지고 처리 해야함 
               ex) gClass.str.SrmInfo[srmNum] : 각 호기의 정보 get / set
                   gClass.str.SrmInfo[gClass.srmNum] : 현재 선택 된 호기의 정보를 get / set
             */

            Console.WriteLine("Create Called Singletone class");

            // 구조체 배열 동적할당
            Srm_Info[] srmInfo = new Srm_Info[3];
            Dio_Packet[] dioPacket = new Dio_Packet[3];
            Srm_Packet[] srmPacket = new Srm_Packet[3];

            //-------------------DIO COMMUNICATION MEMORY--------------------------
            dioPacket[0].DIBIT = new bool[cConstDefine.IOCOUNT];
            dioPacket[0].DOCMD = new bool[cConstDefine.IOCOUNT];
            dioPacket[0].DOBIT = new bool[cConstDefine.IOCOUNT];
            dioPacket[1].DIBIT = new bool[cConstDefine.IOCOUNT];
            dioPacket[1].DOCMD = new bool[cConstDefine.IOCOUNT];
            dioPacket[1].DOBIT = new bool[cConstDefine.IOCOUNT];
            dioPacket[2].DIBIT = new bool[cConstDefine.IOCOUNT];
            dioPacket[2].DOCMD = new bool[cConstDefine.IOCOUNT];
            dioPacket[2].DOBIT = new bool[cConstDefine.IOCOUNT];
            dioPacket[0].DISET = new Dio_Setting[Enum.GetValues(typeof (DISTATE)).Length];
            dioPacket[0].DOSET = new Dio_Setting[Enum.GetValues(typeof (DOSTATE)).Length];
            dioPacket[1].DISET = new Dio_Setting[Enum.GetValues(typeof (DISTATE)).Length];
            dioPacket[1].DOSET = new Dio_Setting[Enum.GetValues(typeof (DOSTATE)).Length];
            dioPacket[2].DISET = new Dio_Setting[Enum.GetValues(typeof (DISTATE)).Length];
            dioPacket[2].DOSET = new Dio_Setting[Enum.GetValues(typeof (DOSTATE)).Length];

            Srm_State[] srmState = new Srm_State[3];

            //-------------------WCS COMMUNICATION MEMORY--------------------------
            Wcs_Packet[] wcsPacket = new Wcs_Packet[3];
            wcsPacket[0].WCSTO = new ushort[cConstDefine.WCSTO];         // GCP -> WCS Word
            wcsPacket[1].WCSTO = new ushort[cConstDefine.WCSTO];
            wcsPacket[2].WCSTO = new ushort[cConstDefine.WCSTO];
            wcsPacket[0].WCSTOBUF = new ushort[cConstDefine.WCSTO];
            wcsPacket[1].WCSTOBUF = new ushort[cConstDefine.WCSTO];
            wcsPacket[2].WCSTOBUF = new ushort[cConstDefine.WCSTO];
            wcsPacket[0].WCSFROMBUF = new ushort[cConstDefine.WCSFROM];         // WCS -> GCP Word Buf
            wcsPacket[1].WCSFROMBUF = new ushort[cConstDefine.WCSFROM];
            wcsPacket[2].WCSFROMBUF = new ushort[cConstDefine.WCSFROM];
            wcsPacket[0].WCSFROM = new ushort[cConstDefine.WCSFROM];         // WCS -> GCP Word
            wcsPacket[1].WCSFROM = new ushort[cConstDefine.WCSFROM];
            wcsPacket[2].WCSFROM = new ushort[cConstDefine.WCSFROM];


            SRM_IO[] srmIO = new SRM_IO[3];

            // Rack Cell 데이터 할당 처리
            srmInfo[0].cellBay = new int[256];
            srmInfo[1].cellBay = new int[256];
            srmInfo[2].cellBay = new int[256];

            srmInfo[0].cellLev = new int[128];
            srmInfo[1].cellLev = new int[128];
            srmInfo[2].cellLev = new int[128];

            srmInfo[0].prohRack = new string[100];
            srmInfo[1].prohRack = new string[100];
            srmInfo[2].prohRack = new string[100];

            srmInfo[0].prohDataList = new int[500][];
            srmInfo[1].prohDataList = new int[500][];
            srmInfo[2].prohDataList = new int[500][];
            for (int i = 0; i < 500; i++)
            {
                srmInfo[0].prohDataList[i] = new int[3];
                srmInfo[1].prohDataList[i] = new int[3];
                srmInfo[2].prohDataList[i] = new int[3];
            }


            srmInfo[0].SrmStation = new Srm_Station[50];
            srmInfo[1].SrmStation = new Srm_Station[50];
            srmInfo[2].SrmStation = new Srm_Station[50];

            
            SharedData = new SharedStruct();
            SharedData.SrmInfo = srmInfo;
            SharedData.DioPacket = dioPacket;
            SharedData.SrmPacket = srmPacket;
            SharedData.SrmState = srmState;
            SharedData.WcsPacket = wcsPacket;
            SharedData.SRMIO = srmIO;

            Console.WriteLine("Create Called Finished Singletone class");
        }

        public ref SharedStruct str { get { return ref SharedData; } }


        public int srmNum { get; set; }                                     // 현재 프로그램 상에서 선택한 호기
    }
}
