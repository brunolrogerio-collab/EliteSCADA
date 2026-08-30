#include "open62541.h"

#include <signal.h>
#include <stdio.h>

#define LAB_NAMESPACE_INDEX 2

static volatile UA_Boolean running = true;

static void
stopHandler(int signalNumber) {
    (void)signalNumber;
    running = false;
}

static UA_StatusCode
addDoubleVariable(UA_Server *server, const char *nodeName, const char *displayName,
                  UA_Double initialValue) {
    UA_VariableAttributes attr = UA_VariableAttributes_default;
    UA_Variant_setScalar(&attr.value, &initialValue, &UA_TYPES[UA_TYPES_DOUBLE]);
    attr.displayName = UA_LOCALIZEDTEXT("en-US", (char *)displayName);
    attr.description = UA_LOCALIZEDTEXT("en-US", (char *)displayName);
    attr.dataType = UA_TYPES[UA_TYPES_DOUBLE].typeId;
    attr.accessLevel = UA_ACCESSLEVELMASK_READ | UA_ACCESSLEVELMASK_WRITE;

    return UA_Server_addVariableNode(
        server,
        UA_NODEID_STRING(LAB_NAMESPACE_INDEX, (char *)nodeName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_OBJECTSFOLDER),
        UA_NODEID_NUMERIC(0, UA_NS0ID_ORGANIZES),
        UA_QUALIFIEDNAME(LAB_NAMESPACE_INDEX, (char *)displayName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_BASEDATAVARIABLETYPE),
        attr,
        NULL,
        NULL);
}

static UA_StatusCode
addInt32Variable(UA_Server *server, const char *nodeName, const char *displayName,
                 UA_Int32 initialValue) {
    UA_VariableAttributes attr = UA_VariableAttributes_default;
    UA_Variant_setScalar(&attr.value, &initialValue, &UA_TYPES[UA_TYPES_INT32]);
    attr.displayName = UA_LOCALIZEDTEXT("en-US", (char *)displayName);
    attr.description = UA_LOCALIZEDTEXT("en-US", (char *)displayName);
    attr.dataType = UA_TYPES[UA_TYPES_INT32].typeId;
    attr.accessLevel = UA_ACCESSLEVELMASK_READ | UA_ACCESSLEVELMASK_WRITE;

    return UA_Server_addVariableNode(
        server,
        UA_NODEID_STRING(LAB_NAMESPACE_INDEX, (char *)nodeName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_OBJECTSFOLDER),
        UA_NODEID_NUMERIC(0, UA_NS0ID_ORGANIZES),
        UA_QUALIFIEDNAME(LAB_NAMESPACE_INDEX, (char *)displayName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_BASEDATAVARIABLETYPE),
        attr,
        NULL,
        NULL);
}

static UA_StatusCode
addBooleanVariable(UA_Server *server, const char *nodeName, const char *displayName,
                   UA_Boolean initialValue) {
    UA_VariableAttributes attr = UA_VariableAttributes_default;
    UA_Variant_setScalar(&attr.value, &initialValue, &UA_TYPES[UA_TYPES_BOOLEAN]);
    attr.displayName = UA_LOCALIZEDTEXT("en-US", (char *)displayName);
    attr.description = UA_LOCALIZEDTEXT("en-US", (char *)displayName);
    attr.dataType = UA_TYPES[UA_TYPES_BOOLEAN].typeId;
    attr.accessLevel = UA_ACCESSLEVELMASK_READ | UA_ACCESSLEVELMASK_WRITE;

    return UA_Server_addVariableNode(
        server,
        UA_NODEID_STRING(LAB_NAMESPACE_INDEX, (char *)nodeName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_OBJECTSFOLDER),
        UA_NODEID_NUMERIC(0, UA_NS0ID_ORGANIZES),
        UA_QUALIFIEDNAME(LAB_NAMESPACE_INDEX, (char *)displayName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_BASEDATAVARIABLETYPE),
        attr,
        NULL,
        NULL);
}

static UA_StatusCode
addStringVariable(UA_Server *server, const char *nodeName, const char *displayName,
                  const char *initialValue) {
    UA_VariableAttributes attr = UA_VariableAttributes_default;
    UA_String value = UA_STRING((char *)initialValue);
    UA_Variant_setScalar(&attr.value, &value, &UA_TYPES[UA_TYPES_STRING]);
    attr.displayName = UA_LOCALIZEDTEXT("en-US", (char *)displayName);
    attr.description = UA_LOCALIZEDTEXT("en-US", (char *)displayName);
    attr.dataType = UA_TYPES[UA_TYPES_STRING].typeId;
    attr.accessLevel = UA_ACCESSLEVELMASK_READ | UA_ACCESSLEVELMASK_WRITE;

    return UA_Server_addVariableNode(
        server,
        UA_NODEID_STRING(LAB_NAMESPACE_INDEX, (char *)nodeName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_OBJECTSFOLDER),
        UA_NODEID_NUMERIC(0, UA_NS0ID_ORGANIZES),
        UA_QUALIFIEDNAME(LAB_NAMESPACE_INDEX, (char *)displayName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_BASEDATAVARIABLETYPE),
        attr,
        NULL,
        NULL);
}

static int
ensureGood(const char *operation, UA_StatusCode status) {
    if(status == UA_STATUSCODE_GOOD)
        return 1;

    fprintf(stderr, "%s failed: %s\n", operation, UA_StatusCode_name(status));
    return 0;
}

int
main(void) {
    signal(SIGINT, stopHandler);
    signal(SIGTERM, stopHandler);

    UA_Server *server = UA_Server_new();
    if(!server) {
        fprintf(stderr, "Failed to create open62541 server\n");
        return 1;
    }

    UA_UInt16 namespaceIndex = UA_Server_addNamespace(server, "urn:elitescada:interop:opcua");
    if(namespaceIndex != LAB_NAMESPACE_INDEX) {
        fprintf(stderr, "Expected interoperability namespace index %u, got %u\n",
                (unsigned)LAB_NAMESPACE_INDEX, (unsigned)namespaceIndex);
        UA_Server_delete(server);
        return 1;
    }

    int ok = 1;
    ok &= ensureGood("add Lab.Temperature",
                     addDoubleVariable(server, "Lab.Temperature", "Lab Temperature", 21.5));
    ok &= ensureGood("add Lab.Counter",
                     addInt32Variable(server, "Lab.Counter", "Lab Counter", 0));
    ok &= ensureGood("add Lab.Active",
                     addBooleanVariable(server, "Lab.Active", "Lab Active", true));
    ok &= ensureGood("add Lab.MachineName",
                     addStringVariable(server, "Lab.MachineName", "Lab Machine Name", "EliteSCADA Lab"));

    if(!ok) {
        UA_Server_delete(server);
        return 1;
    }

    printf("EliteSCADA OPC UA peer ready on opc.tcp://0.0.0.0:4840\n");
    fflush(stdout);

    UA_StatusCode status = UA_Server_run(server, &running);
    UA_Server_delete(server);
    return status == UA_STATUSCODE_GOOD ? 0 : 1;
}
