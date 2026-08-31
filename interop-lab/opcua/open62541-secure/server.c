#include "open62541.h"
#include <signal.h>
#include <stdio.h>

#define LAB_NAMESPACE_INDEX 2
static volatile UA_Boolean running = true;

static UA_UsernamePasswordLogin userLogins[1] = {
    {UA_STRING_STATIC("elite-user"), UA_STRING_STATIC("elite-pass")}
};

static void stopHandler(int signalNumber) { (void)signalNumber; running = false; }

static UA_ByteString loadFile(const char *path) {
    UA_ByteString data = UA_BYTESTRING_NULL;
    FILE *file = fopen(path, "rb");
    if(!file) return data;
    fseek(file, 0, SEEK_END);
    long length = ftell(file);
    rewind(file);
    if(length <= 0 || UA_ByteString_allocBuffer(&data, (size_t)length) != UA_STATUSCODE_GOOD) {
        fclose(file);
        return UA_BYTESTRING_NULL;
    }
    size_t read = fread(data.data, 1, data.length, file);
    fclose(file);
    if(read != data.length) { UA_ByteString_clear(&data); return UA_BYTESTRING_NULL; }
    return data;
}

static UA_StatusCode addInt32Variable(UA_Server *server, const char *nodeName, const char *displayName, UA_Int32 initialValue) {
    UA_VariableAttributes attr = UA_VariableAttributes_default;
    UA_Variant_setScalar(&attr.value, &initialValue, &UA_TYPES[UA_TYPES_INT32]);
    attr.displayName = UA_LOCALIZEDTEXT("en-US", (char *)displayName);
    attr.dataType = UA_TYPES[UA_TYPES_INT32].typeId;
    attr.accessLevel = UA_ACCESSLEVELMASK_READ | UA_ACCESSLEVELMASK_WRITE;
    return UA_Server_addVariableNode(server,
        UA_NODEID_STRING(LAB_NAMESPACE_INDEX, (char *)nodeName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_OBJECTSFOLDER),
        UA_NODEID_NUMERIC(0, UA_NS0ID_ORGANIZES),
        UA_QUALIFIEDNAME(LAB_NAMESPACE_INDEX, (char *)displayName),
        UA_NODEID_NUMERIC(0, UA_NS0ID_BASEDATAVARIABLETYPE), attr, NULL, NULL);
}

int main(int argc, char **argv) {
    if(argc != 4) {
        fprintf(stderr, "usage: %s <server-cert.der> <server-key.der> <trusted-client-cert.der>\n", argv[0]);
        return 2;
    }
    signal(SIGINT, stopHandler);
    signal(SIGTERM, stopHandler);

    UA_ByteString certificate = loadFile(argv[1]);
    UA_ByteString privateKey = loadFile(argv[2]);
    UA_ByteString trustedClient = loadFile(argv[3]);
    if(!certificate.length || !privateKey.length || !trustedClient.length) {
        fprintf(stderr, "failed to load secure peer material\n");
        return 3;
    }

    UA_Server *server = UA_Server_new();
    if(!server) return 4;
    UA_ServerConfig *config = UA_Server_getConfig(server);
    UA_StatusCode status = UA_ServerConfig_setDefaultWithSecurityPolicies(
        config, 4840, &certificate, &privateKey, &trustedClient, 1, NULL, 0, NULL, 0);
    UA_ByteString_clear(&certificate);
    UA_ByteString_clear(&privateKey);
    UA_ByteString_clear(&trustedClient);
    if(status != UA_STATUSCODE_GOOD) {
        fprintf(stderr, "secure server configuration failed: %s\n", UA_StatusCode_name(status));
        UA_Server_delete(server);
        return 5;
    }

    UA_String_clear(&config->applicationDescription.applicationUri);
    config->applicationDescription.applicationUri = UA_STRING_ALLOC("urn:elitescada:interop:opcua:server");

    /* Keep anonymous for the certificate-protected baseline. Username is encrypted
       with Basic256Sha256. The default access-control plugin also advertises X.509
       user identity and validates it through the server session PKI. */
    config->allowNonePolicyPassword = false;
    config->accessControl.clear(&config->accessControl);
    const UA_String userTokenPolicy = UA_STRING("http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256");
    status = UA_AccessControl_default(config, true, &userTokenPolicy, 1, userLogins);
    if(status != UA_STATUSCODE_GOOD) {
        fprintf(stderr, "secure access control configuration failed: %s\n", UA_StatusCode_name(status));
        UA_Server_delete(server);
        return 6;
    }

    UA_UInt16 namespaceIndex = UA_Server_addNamespace(server, "urn:elitescada:interop:opcua");
    if(namespaceIndex != LAB_NAMESPACE_INDEX ||
       addInt32Variable(server, "Lab.SecureCounter", "Secure Counter", 7) != UA_STATUSCODE_GOOD ||
       addInt32Variable(server, "Lab.UserCounter", "User Counter", 17) != UA_STATUSCODE_GOOD ||
       addInt32Variable(server, "Lab.CertificateCounter", "Certificate Counter", 27) != UA_STATUSCODE_GOOD ||
       addInt32Variable(server, "Lab.RecoveryCounter", "Recovery Counter", 37) != UA_STATUSCODE_GOOD) {
        UA_Server_delete(server);
        return 7;
    }

    printf("EliteSCADA secure OPC UA peer ready on opc.tcp://0.0.0.0:4840\n");
    fflush(stdout);
    status = UA_Server_run(server, &running);
    UA_Server_delete(server);
    return status == UA_STATUSCODE_GOOD ? 0 : 1;
}
