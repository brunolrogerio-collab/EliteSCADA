#include <signal.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>

#include "cs104_slave.h"
#include "hal_thread.h"
#include "hal_time.h"

static volatile sig_atomic_t g_running = 1;
static volatile bool g_activated = false;

static void handleSignal(int signalId)
{
    (void)signalId;
    g_running = 0;
}

static bool interrogationHandler(void* parameter, IMasterConnection connection, CS101_ASDU request, uint8_t qoi)
{
    (void)parameter;

    if (CS101_ASDU_getCA(request) != 1 || qoi != 20)
    {
        IMasterConnection_sendACT_CON(connection, request, true);
        return true;
    }

    IMasterConnection_sendACT_CON(connection, request, false);

    CS101_AppLayerParameters alParams = IMasterConnection_getApplicationLayerParameters(connection);
    CS101_ASDU data = CS101_ASDU_create(alParams, false, CS101_COT_INTERROGATED_BY_STATION,
        (uint8_t)CS101_ASDU_getOA(request), 1, false, false);

    InformationObject scaled = (InformationObject)MeasuredValueScaled_create(NULL, 100, 23, IEC60870_QUALITY_GOOD);
    CS101_ASDU_addInformationObject(data, scaled);
    InformationObject_destroy(scaled);
    IMasterConnection_sendASDU(connection, data);
    CS101_ASDU_destroy(data);

    data = CS101_ASDU_create(alParams, false, CS101_COT_INTERROGATED_BY_STATION,
        (uint8_t)CS101_ASDU_getOA(request), 1, false, false);
    InformationObject single = (InformationObject)SinglePointInformation_create(NULL, 104, true, IEC60870_QUALITY_GOOD);
    CS101_ASDU_addInformationObject(data, single);
    InformationObject_destroy(single);
    IMasterConnection_sendASDU(connection, data);
    CS101_ASDU_destroy(data);

    IMasterConnection_sendACT_TERM(connection, request);
    return true;
}

static bool isSupportedCommandType(int typeId)
{
    return typeId == C_SC_NA_1 || typeId == C_DC_NA_1 || typeId == C_SE_NA_1 ||
           typeId == C_SE_NB_1 || typeId == C_SE_NC_1;
}

static bool commandIsSelect(int typeId, InformationObject io)
{
    switch (typeId)
    {
        case C_SC_NA_1: return SingleCommand_isSelect((SingleCommand)io);
        case C_DC_NA_1: return DoubleCommand_isSelect((DoubleCommand)io);
        case C_SE_NA_1: return SetpointCommandNormalized_isSelect((SetpointCommandNormalized)io);
        case C_SE_NB_1: return SetpointCommandScaled_isSelect((SetpointCommandScaled)io);
        case C_SE_NC_1: return SetpointCommandShort_isSelect((SetpointCommandShort)io);
        default: return false;
    }
}

static bool asduHandler(void* parameter, IMasterConnection connection, CS101_ASDU asdu)
{
    (void)parameter;
    int typeId = (int)CS101_ASDU_getTypeID(asdu);
    if (!isSupportedCommandType(typeId))
        return false;

    bool accepted = false;
    bool selectPhase = false;
    bool singleState = false;
    bool publishSingleFeedback = false;

    if (CS101_ASDU_getCA(asdu) == 1 && CS101_ASDU_getCOT(asdu) == CS101_COT_ACTIVATION)
    {
        InformationObject io = CS101_ASDU_getElement(asdu, 0);
        if (io != NULL)
        {
            if (InformationObject_getObjectAddress(io) == 5000)
            {
                accepted = true;
                selectPhase = commandIsSelect(typeId, io);
                if (typeId == C_SC_NA_1)
                {
                    singleState = SingleCommand_getState((SingleCommand)io);
                    publishSingleFeedback = !selectPhase;
                }
            }
            InformationObject_destroy(io);
        }
    }

    IMasterConnection_sendACT_CON(connection, asdu, !accepted);
    if (!accepted || selectPhase)
        return true;

    IMasterConnection_sendACT_TERM(connection, asdu);

    if (publishSingleFeedback)
    {
        CS101_AppLayerParameters alParams = IMasterConnection_getApplicationLayerParameters(connection);
        CS101_ASDU feedback = CS101_ASDU_create(alParams, false, CS101_COT_SPONTANEOUS, 0, 1, false, false);
        InformationObject single = (InformationObject)SinglePointInformation_create(NULL, 5001, singleState, IEC60870_QUALITY_GOOD);
        CS101_ASDU_addInformationObject(feedback, single);
        InformationObject_destroy(single);
        IMasterConnection_sendASDU(connection, feedback);
        CS101_ASDU_destroy(feedback);
    }

    return true;
}

static void connectionEventHandler(void* parameter, IMasterConnection connection, CS104_PeerConnectionEvent event)
{
    (void)parameter;
    (void)connection;
    if (event == CS104_CON_EVENT_ACTIVATED)
        g_activated = true;
    else if (event == CS104_CON_EVENT_DEACTIVATED || event == CS104_CON_EVENT_CONNECTION_CLOSED)
        g_activated = false;
}

int main(void)
{
    signal(SIGINT, handleSignal);
    signal(SIGTERM, handleSignal);

    CS104_Slave slave = CS104_Slave_create(32, 32);
    if (slave == NULL)
        return 2;

    CS104_Slave_setLocalAddress(slave, "0.0.0.0");
    CS104_Slave_setLocalPort(slave, 2404);
    CS104_Slave_setServerMode(slave, CS104_MODE_SINGLE_REDUNDANCY_GROUP);
    CS104_Slave_setMaxOpenConnections(slave, 2);
    CS104_Slave_setInterrogationHandler(slave, interrogationHandler, NULL);
    CS104_Slave_setASDUHandler(slave, asduHandler, NULL);
    CS104_Slave_setConnectionEventHandler(slave, connectionEventHandler, NULL);

    CS104_Slave_start(slave);
    if (!CS104_Slave_isRunning(slave))
    {
        CS104_Slave_destroy(slave);
        return 3;
    }

    printf("EliteSCADA IEC-104 lab peer listening on 0.0.0.0:2404\n");
    fflush(stdout);

    int16_t spontaneousValue = 1000;
    uint64_t lastSpontaneous = 0;
    while (g_running)
    {
        uint64_t now = Hal_getMonotonicTimeInMs();
        if (g_activated && now >= lastSpontaneous + 500)
        {
            lastSpontaneous = now;
            CS101_AppLayerParameters alParams = CS104_Slave_getAppLayerParameters(slave);
            CS101_ASDU spontaneous = CS101_ASDU_create(alParams, false, CS101_COT_SPONTANEOUS, 0, 1, false, false);
            InformationObject scaled = (InformationObject)MeasuredValueScaled_create(NULL, 110, spontaneousValue++, IEC60870_QUALITY_GOOD);
            CS101_ASDU_addInformationObject(spontaneous, scaled);
            InformationObject_destroy(scaled);
            CS104_Slave_enqueueASDU(slave, spontaneous);
            CS101_ASDU_destroy(spontaneous);
        }
        Thread_sleep(10);
    }

    CS104_Slave_stop(slave);
    CS104_Slave_destroy(slave);
    return 0;
}
