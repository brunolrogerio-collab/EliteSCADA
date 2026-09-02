#include <opendnp3/DNP3Manager.h>
#include <opendnp3/app/AnalogOutput.h>
#include <opendnp3/app/ControlRelayOutputBlock.h>
#include <opendnp3/app/IINField.h>
#include <opendnp3/channel/ChannelRetry.h>
#include <opendnp3/channel/IChannelListener.h>
#include <opendnp3/gen/ChannelState.h>
#include <opendnp3/gen/CommandPointState.h>
#include <opendnp3/gen/CommandStatus.h>
#include <opendnp3/gen/MasterTaskType.h>
#include <opendnp3/gen/OperationType.h>
#include <opendnp3/gen/TaskCompletion.h>
#include <opendnp3/gen/TimeSyncMode.h>
#include <opendnp3/gen/TimestampQuality.h>
#include <opendnp3/gen/TripCloseCode.h>
#include <opendnp3/logging/LogLevels.h>
#include <opendnp3/master/ICommandTaskResult.h>
#include <opendnp3/master/IMasterApplication.h>
#include <opendnp3/master/IMasterScan.h>
#include <opendnp3/master/ISOEHandler.h>
#include <opendnp3/master/ITaskCallback.h>
#include <opendnp3/master/MasterStackConfig.h>
#include <opendnp3/master/TaskConfig.h>
#include <opendnp3/util/TimeDuration.h>
#include <opendnp3/util/UTCTimestamp.h>

#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstdlib>
#include <iomanip>
#include <iostream>
#include <limits>
#include <locale>
#include <memory>
#include <mutex>
#include <sstream>
#include <stdexcept>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

namespace
{
using namespace opendnp3;

constexpr const char* ProtocolVersion = "V1";

struct Config
{
    std::string host;
    uint16_t port = 20000;
    uint16_t masterAddress = 1;
    uint16_t outstationAddress = 1024;
    int64_t responseTimeoutMs = 5000;
    int64_t reconnectMinMs = 1000;
    int64_t reconnectMaxMs = 30000;
    int64_t keepAliveMs = 60000;
    uint8_t startupClasses = ClassField::ALL_CLASSES;
    uint8_t disableUnsolicitedClasses = ClassField::EVENT_CLASSES;
    uint8_t enableUnsolicitedClasses = ClassField::EVENT_CLASSES;
    uint8_t eventScanClasses = ClassField::EVENT_CLASSES;
    int64_t integrityPollMs = 900000;
    int64_t class1PollMs = -1;
    int64_t class2PollMs = -1;
    int64_t class3PollMs = -1;
    bool integrityOnOverflow = true;
    std::string timeSync = "Disabled";
    int maxQueuedUserRequests = 16;
};

std::vector<std::string> SplitTabs(const std::string& line)
{
    std::vector<std::string> fields;
    std::size_t start = 0;
    while (true)
    {
        const auto next = line.find('\t', start);
        if (next == std::string::npos)
        {
            fields.emplace_back(line.substr(start));
            return fields;
        }
        fields.emplace_back(line.substr(start, next - start));
        start = next + 1;
    }
}

template <class T> T ParseInteger(const std::string& value, const char* name)
{
    std::size_t consumed = 0;
    long long parsed = 0;
    try
    {
        parsed = std::stoll(value, &consumed, 10);
    }
    catch (const std::exception&)
    {
        throw std::invalid_argument(std::string("Invalid ") + name + ": " + value);
    }
    if (consumed != value.size() || parsed < static_cast<long long>(std::numeric_limits<T>::min())
        || parsed > static_cast<long long>(std::numeric_limits<T>::max()))
    {
        throw std::out_of_range(std::string("Out-of-range ") + name + ": " + value);
    }
    return static_cast<T>(parsed);
}

template <> uint64_t ParseInteger<uint64_t>(const std::string& value, const char* name)
{
    std::size_t consumed = 0;
    unsigned long long parsed = 0;
    try
    {
        parsed = std::stoull(value, &consumed, 10);
    }
    catch (const std::exception&)
    {
        throw std::invalid_argument(std::string("Invalid ") + name + ": " + value);
    }
    if (consumed != value.size())
        throw std::invalid_argument(std::string("Invalid ") + name + ": " + value);
    return static_cast<uint64_t>(parsed);
}

bool ParseBool(const std::string& value, const char* name)
{
    if (value == "1" || value == "true" || value == "TRUE") return true;
    if (value == "0" || value == "false" || value == "FALSE") return false;
    throw std::invalid_argument(std::string("Invalid ") + name + ": " + value);
}

std::unordered_map<std::string, std::string> ParseOptions(int argc, char** argv)
{
    std::unordered_map<std::string, std::string> result;
    if (((argc - 1) % 2) != 0) throw std::invalid_argument("Every native host option requires a value.");
    for (int i = 1; i < argc; i += 2)
    {
        const std::string name(argv[i]);
        if (name.rfind("--", 0) != 0) throw std::invalid_argument("Native host option names must begin with --.");
        if (!result.emplace(name, argv[i + 1]).second) throw std::invalid_argument("Duplicate native host option: " + name);
    }
    return result;
}

const std::string& Require(const std::unordered_map<std::string, std::string>& options, const std::string& name)
{
    const auto found = options.find(name);
    if (found == options.end()) throw std::invalid_argument("Missing native host option: " + name);
    return found->second;
}

Config ParseConfig(int argc, char** argv)
{
    const auto options = ParseOptions(argc, argv);
    if (Require(options, "--protocol") != ProtocolVersion) throw std::invalid_argument("Unsupported native host protocol version.");

    Config config;
    config.host = Require(options, "--host");
    config.port = ParseInteger<uint16_t>(Require(options, "--port"), "port");
    config.masterAddress = ParseInteger<uint16_t>(Require(options, "--master-address"), "master address");
    config.outstationAddress = ParseInteger<uint16_t>(Require(options, "--outstation-address"), "outstation address");
    config.responseTimeoutMs = ParseInteger<int64_t>(Require(options, "--response-timeout-ms"), "response timeout");
    config.reconnectMinMs = ParseInteger<int64_t>(Require(options, "--reconnect-min-ms"), "minimum reconnect delay");
    config.reconnectMaxMs = ParseInteger<int64_t>(Require(options, "--reconnect-max-ms"), "maximum reconnect delay");
    config.keepAliveMs = ParseInteger<int64_t>(Require(options, "--keep-alive-ms"), "keep alive timeout");
    config.startupClasses = ParseInteger<uint8_t>(Require(options, "--startup-classes"), "startup classes");
    config.disableUnsolicitedClasses = ParseInteger<uint8_t>(Require(options, "--disable-unsolicited-classes"), "disable unsolicited classes");
    config.enableUnsolicitedClasses = ParseInteger<uint8_t>(Require(options, "--enable-unsolicited-classes"), "enable unsolicited classes");
    config.eventScanClasses = ParseInteger<uint8_t>(Require(options, "--event-scan-classes"), "event scan classes");
    config.integrityPollMs = ParseInteger<int64_t>(Require(options, "--integrity-poll-ms"), "integrity poll interval");
    config.class1PollMs = ParseInteger<int64_t>(Require(options, "--class1-poll-ms"), "class 1 poll interval");
    config.class2PollMs = ParseInteger<int64_t>(Require(options, "--class2-poll-ms"), "class 2 poll interval");
    config.class3PollMs = ParseInteger<int64_t>(Require(options, "--class3-poll-ms"), "class 3 poll interval");
    config.integrityOnOverflow = ParseBool(Require(options, "--integrity-on-overflow"), "integrity-on-overflow");
    config.timeSync = Require(options, "--time-sync");
    config.maxQueuedUserRequests = ParseInteger<int>(Require(options, "--max-queued-user-requests"), "maximum queued user requests");

    if (config.host.empty()) throw std::invalid_argument("DNP3 host is required.");
    if (config.port == 0) throw std::invalid_argument("DNP3 port must be non-zero.");
    if (config.masterAddress == config.outstationAddress) throw std::invalid_argument("DNP3 master and outstation addresses must differ.");
    if (config.responseTimeoutMs <= 0 || config.reconnectMinMs <= 0 || config.reconnectMaxMs < config.reconnectMinMs)
        throw std::invalid_argument("DNP3 timeout/reconnect configuration is invalid.");
    if (config.startupClasses == 0 || (config.startupClasses & ~ClassField::ALL_CLASSES) != 0)
        throw std::invalid_argument("DNP3 startup classes are invalid.");
    if ((config.disableUnsolicitedClasses & ~ClassField::EVENT_CLASSES) != 0
        || (config.enableUnsolicitedClasses & ~ClassField::EVENT_CLASSES) != 0
        || (config.eventScanClasses & ~ClassField::EVENT_CLASSES) != 0)
        throw std::invalid_argument("DNP3 event class masks are invalid.");
    if (config.disableUnsolicitedClasses != 0 && config.disableUnsolicitedClasses != config.enableUnsolicitedClasses)
        throw std::invalid_argument("OpenDNP3 3.1.2 requires the startup-disable and post-integrity unsolicited class masks to match.");
    if (config.maxQueuedUserRequests <= 0)
        throw std::invalid_argument("DNP3 maximum queued user requests must be positive.");
    return config;
}

class ProtocolWriter
{
public:
    void Ready() { Write({ProtocolVersion, "READY"}); }
    void State(const std::string& state) { Write({ProtocolVersion, "STATE", state}); }
    void Diagnostic(const std::string& kind) { Write({ProtocolVersion, "DIAGNOSTIC", kind}); }

    void Command(uint64_t requestId, bool success, const std::string& status)
    {
        Write({ProtocolVersion, "COMMAND", std::to_string(requestId), success ? "1" : "0", status, ""});
    }

    void Measurement(const HeaderInfo& info,
                     const std::string& kind,
                     uint16_t index,
                     uint8_t flags,
                     uint64_t timestamp,
                     const std::string& type,
                     const std::string& value,
                     bool chatter,
                     bool overRange,
                     bool rollover,
                     bool discontinuity,
                     bool referenceError)
    {
        const auto encoded = static_cast<uint16_t>(info.gv);
        const auto group = static_cast<uint8_t>((encoded >> 8) & 0xFF);
        const auto variation = static_cast<uint8_t>(encoded & 0xFF);
        const bool hasTimestamp = info.tsquality != TimestampQuality::INVALID;
        const bool synchronized = info.tsquality == TimestampQuality::SYNCHRONIZED;
        Write({ProtocolVersion,
               "MEASUREMENT",
               kind,
               std::to_string(index),
               std::to_string(group),
               std::to_string(variation),
               info.isEventVariation ? "1" : "0",
               info.flagsValid ? "1" : "0",
               (flags & 0x01) ? "1" : "0",
               (flags & 0x02) ? "1" : "0",
               (flags & 0x04) ? "1" : "0",
               (flags & 0x08) ? "1" : "0",
               (flags & 0x10) ? "1" : "0",
               chatter ? "1" : "0",
               overRange ? "1" : "0",
               rollover ? "1" : "0",
               discontinuity ? "1" : "0",
               referenceError ? "1" : "0",
               hasTimestamp ? std::to_string(timestamp) : "",
               synchronized ? "1" : "0",
               type,
               value});
    }

private:
    void Write(const std::vector<std::string>& fields)
    {
        std::lock_guard<std::mutex> lock(gate_);
        for (std::size_t i = 0; i < fields.size(); ++i)
        {
            if (i != 0) std::cout << '\t';
            std::cout << fields[i];
        }
        std::cout << '\n' << std::flush;
    }

    std::mutex gate_;
};

template <class T> std::string Number(T value)
{
    std::ostringstream output;
    output.imbue(std::locale::classic());
    if constexpr (std::is_floating_point_v<T>) output << std::setprecision(std::numeric_limits<T>::max_digits10);
    output << value;
    return output.str();
}

std::pair<std::string, std::string> AnalogValue(uint8_t group, uint8_t variation, double value)
{
    if ((group == 30 && (variation == 1 || variation == 3)) || (group == 32 && (variation == 1 || variation == 3)))
        return {"i32", Number(static_cast<int32_t>(value))};
    if ((group == 30 && (variation == 2 || variation == 4)) || (group == 32 && (variation == 2 || variation == 4)))
        return {"i16", Number(static_cast<int16_t>(value))};
    if ((group == 30 && variation == 5) || (group == 32 && (variation == 5 || variation == 7)))
        return {"f32", Number(static_cast<float>(value))};
    return {"f64", Number(value)};
}

std::pair<std::string, std::string> AnalogOutputStatusValue(uint8_t group, uint8_t variation, double value)
{
    if ((group == 40 && variation == 1) || (group == 42 && (variation == 1 || variation == 3)))
        return {"i32", Number(static_cast<int32_t>(value))};
    if ((group == 40 && variation == 2) || (group == 42 && (variation == 2 || variation == 4)))
        return {"i16", Number(static_cast<int16_t>(value))};
    if ((group == 40 && variation == 3) || (group == 42 && (variation == 5 || variation == 7)))
        return {"f32", Number(static_cast<float>(value))};
    return {"f64", Number(value)};
}

std::pair<std::string, std::string> CounterValue(uint8_t variation, uint32_t value)
{
    if (variation == 2 || variation == 6) return {"i32", Number(static_cast<int32_t>(value & 0xFFFFu))};
    return {"i64", Number(static_cast<int64_t>(value))};
}

class EliteSOEHandler final : public ISOEHandler
{
public:
    explicit EliteSOEHandler(ProtocolWriter& writer) : writer_(writer) {}

    void BeginFragment(const ResponseInfo& info) override
    {
        if (info.unsolicited && info.fir) writer_.Diagnostic("UNSOLICITED");
    }

    void EndFragment(const ResponseInfo&) override {}

    void Process(const HeaderInfo& info, const ICollection<Indexed<Binary>>& values) override
    {
        values.ForeachItem([&](const auto& item) {
            writer_.Measurement(info, "BinaryInput", item.index, item.value.flags.value, item.value.time.value,
                                "bool", item.value.value ? "true" : "false", (item.value.flags.value & 0x20) != 0,
                                false, false, false, false);
        });
    }

    void Process(const HeaderInfo& info, const ICollection<Indexed<DoubleBitBinary>>& values) override
    {
        values.ForeachItem([&](const auto& item) {
            writer_.Measurement(info, "DoubleBitBinaryInput", item.index, item.value.flags.value, item.value.time.value,
                                "enum", Number(static_cast<uint8_t>(item.value.value)), (item.value.flags.value & 0x20) != 0,
                                false, false, false, false);
        });
    }

    void Process(const HeaderInfo& info, const ICollection<Indexed<Analog>>& values) override
    {
        const auto encoded = static_cast<uint16_t>(info.gv);
        const auto group = static_cast<uint8_t>((encoded >> 8) & 0xFF);
        const auto variation = static_cast<uint8_t>(encoded & 0xFF);
        values.ForeachItem([&](const auto& item) {
            const auto wire = AnalogValue(group, variation, item.value.value);
            writer_.Measurement(info, "AnalogInput", item.index, item.value.flags.value, item.value.time.value,
                                wire.first, wire.second, false, (item.value.flags.value & 0x20) != 0,
                                false, false, (item.value.flags.value & 0x40) != 0);
        });
    }

    void Process(const HeaderInfo& info, const ICollection<Indexed<Counter>>& values) override
    {
        const auto variation = static_cast<uint8_t>(static_cast<uint16_t>(info.gv) & 0xFF);
        values.ForeachItem([&](const auto& item) {
            const auto wire = CounterValue(variation, item.value.value);
            writer_.Measurement(info, "Counter", item.index, item.value.flags.value, item.value.time.value,
                                wire.first, wire.second, false, false, (item.value.flags.value & 0x20) != 0,
                                (item.value.flags.value & 0x40) != 0, false);
        });
    }

    void Process(const HeaderInfo& info, const ICollection<Indexed<FrozenCounter>>& values) override
    {
        const auto variation = static_cast<uint8_t>(static_cast<uint16_t>(info.gv) & 0xFF);
        values.ForeachItem([&](const auto& item) {
            const auto wire = CounterValue(variation, item.value.value);
            writer_.Measurement(info, "FrozenCounter", item.index, item.value.flags.value, item.value.time.value,
                                wire.first, wire.second, false, false, (item.value.flags.value & 0x20) != 0,
                                (item.value.flags.value & 0x40) != 0, false);
        });
    }

    void Process(const HeaderInfo& info, const ICollection<Indexed<BinaryOutputStatus>>& values) override
    {
        values.ForeachItem([&](const auto& item) {
            writer_.Measurement(info, "BinaryOutputStatus", item.index, item.value.flags.value, item.value.time.value,
                                "bool", item.value.value ? "true" : "false", (item.value.flags.value & 0x20) != 0,
                                false, false, false, false);
        });
    }

    void Process(const HeaderInfo& info, const ICollection<Indexed<AnalogOutputStatus>>& values) override
    {
        const auto encoded = static_cast<uint16_t>(info.gv);
        const auto group = static_cast<uint8_t>((encoded >> 8) & 0xFF);
        const auto variation = static_cast<uint8_t>(encoded & 0xFF);
        values.ForeachItem([&](const auto& item) {
            const auto wire = AnalogOutputStatusValue(group, variation, item.value.value);
            writer_.Measurement(info, "AnalogOutputStatus", item.index, item.value.flags.value, item.value.time.value,
                                wire.first, wire.second, false, (item.value.flags.value & 0x20) != 0,
                                false, false, (item.value.flags.value & 0x40) != 0);
        });
    }

    void Process(const HeaderInfo&, const ICollection<Indexed<OctetString>>&) override {}
    void Process(const HeaderInfo&, const ICollection<Indexed<TimeAndInterval>>&) override {}
    void Process(const HeaderInfo&, const ICollection<Indexed<BinaryCommandEvent>>&) override {}
    void Process(const HeaderInfo&, const ICollection<Indexed<AnalogCommandEvent>>&) override {}
    void Process(const HeaderInfo&, const ICollection<DNPTime>&) override {}

private:
    ProtocolWriter& writer_;
};

void EmitClassDiagnostics(ProtocolWriter& writer, uint8_t classes)
{
    if ((classes & ClassField::CLASS_0) != 0) writer.Diagnostic("CLASS0_SCAN");
    if ((classes & ClassField::CLASS_1) != 0) writer.Diagnostic("CLASS1_SCAN");
    if ((classes & ClassField::CLASS_2) != 0) writer.Diagnostic("CLASS2_SCAN");
    if ((classes & ClassField::CLASS_3) != 0) writer.Diagnostic("CLASS3_SCAN");
}

class EliteMasterApplication final : public IMasterApplication
{
public:
    EliteMasterApplication(ProtocolWriter& writer, uint8_t startupClasses)
        : writer_(writer), startupClasses_(startupClasses) {}

    UTCTimestamp Now() override
    {
        const auto now = std::chrono::system_clock::now().time_since_epoch();
        return UTCTimestamp(static_cast<uint64_t>(std::chrono::duration_cast<std::chrono::milliseconds>(now).count()));
    }

    void OnReceiveIIN(const IINField& iin) override
    {
        if (iin.IsSet(IINBit::DEVICE_RESTART)) writer_.Diagnostic("DEVICE_RESTART");
        if (iin.IsSet(IINBit::EVENT_BUFFER_OVERFLOW)) writer_.Diagnostic("EVENT_BUFFER_OVERFLOW");
    }

    void OnTaskStart(MasterTaskType type, TaskId) override
    {
        if (type == MasterTaskType::STARTUP_INTEGRITY_POLL) writer_.State("StartupIntegrity");
    }

    void OnTaskComplete(const TaskInfo& info) override
    {
        if (info.type != MasterTaskType::STARTUP_INTEGRITY_POLL) return;
        if (info.result == TaskCompletion::SUCCESS)
        {
            writer_.Diagnostic("STARTUP_INTEGRITY");
            EmitClassDiagnostics(writer_, startupClasses_);
            writer_.State("Online");
        }
        else
        {
            writer_.State("Degraded");
        }
    }

private:
    ProtocolWriter& writer_;
    uint8_t startupClasses_;
};

class ScanDiagnosticCallback final : public ITaskCallback
{
public:
    ScanDiagnosticCallback(ProtocolWriter& writer, std::vector<std::string> diagnostics)
        : writer_(writer), diagnostics_(std::move(diagnostics)) {}

    void OnStart() override {}

    void OnComplete(TaskCompletion result) override
    {
        if (result != TaskCompletion::SUCCESS) return;
        for (const auto& diagnostic : diagnostics_) writer_.Diagnostic(diagnostic);
    }

    void OnDestroyed() override {}

private:
    ProtocolWriter& writer_;
    std::vector<std::string> diagnostics_;
};

class EliteChannelListener final : public IChannelListener
{
public:
    EliteChannelListener(ProtocolWriter& writer, std::atomic<bool>& stopping)
        : writer_(writer), stopping_(stopping) {}

    void OnStateChange(ChannelState state) override
    {
        switch (state)
        {
        case ChannelState::OPENING:
            writer_.State(everOpened_.load() ? "Reconnecting" : "Connecting");
            break;
        case ChannelState::OPEN:
            everOpened_.store(true);
            break;
        case ChannelState::CLOSED:
            writer_.State(everOpened_.load() ? "Reconnecting" : "Connecting");
            break;
        case ChannelState::SHUTDOWN:
            if (!stopping_.load()) writer_.State("Faulted");
            break;
        }
    }

private:
    ProtocolWriter& writer_;
    std::atomic<bool>& stopping_;
    std::atomic<bool> everOpened_{false};
};

OperationType ParseOperation(const std::string& value)
{
    if (value == "LatchOn") return OperationType::LATCH_ON;
    if (value == "LatchOff") return OperationType::LATCH_OFF;
    if (value == "PulseOn") return OperationType::PULSE_ON;
    if (value == "PulseOff") return OperationType::PULSE_OFF;
    throw std::invalid_argument("Unsupported CROB operation: " + value);
}

TripCloseCode ParseTripClose(const std::string& value)
{
    if (value == "None") return TripCloseCode::NUL;
    if (value == "Trip") return TripCloseCode::TRIP;
    if (value == "Close") return TripCloseCode::CLOSE;
    throw std::invalid_argument("Unsupported trip/close code: " + value);
}

void CompleteCommand(ProtocolWriter& writer, uint64_t requestId, const ICommandTaskResult& result)
{
    bool found = false;
    CommandPointState pointState = CommandPointState::INIT;
    CommandStatus pointStatus = CommandStatus::UNDEFINED;
    result.ForeachItem([&](const CommandPointResult& point) {
        if (!found)
        {
            found = true;
            pointState = point.state;
            pointStatus = point.status;
        }
    });

    const bool success = result.summary == TaskCompletion::SUCCESS && found
        && pointState == CommandPointState::SUCCESS && pointStatus == CommandStatus::SUCCESS;
    if (success)
    {
        writer.Command(requestId, true, "SUCCESS");
        return;
    }

    if (found && pointStatus != CommandStatus::SUCCESS)
        writer.Command(requestId, false, CommandStatusSpec::to_string(pointStatus));
    else if (found && pointState != CommandPointState::SUCCESS)
        writer.Command(requestId, false, CommandPointStateSpec::to_string(pointState));
    else
        writer.Command(requestId, false, TaskCompletionSpec::to_string(result.summary));
}

void ExecuteBinary(const std::vector<std::string>& fields, const std::shared_ptr<IMaster>& master, ProtocolWriter& writer)
{
    if (fields.size() != 10) throw std::invalid_argument("BINARY command requires 10 fields.");
    const auto requestId = ParseInteger<uint64_t>(fields[2], "request id");
    const auto index = ParseInteger<uint16_t>(fields[3], "command index");
    const auto operation = ParseOperation(fields[4]);
    const auto mode = fields[5];
    const auto tripClose = ParseTripClose(fields[6]);
    const auto count = ParseInteger<uint8_t>(fields[7], "CROB count");
    const auto onTime = ParseInteger<uint32_t>(fields[8], "CROB on-time");
    const auto offTime = ParseInteger<uint32_t>(fields[9], "CROB off-time");
    ControlRelayOutputBlock command(operation, tripClose, false, count, onTime, offTime);
    auto callback = [&writer, requestId](const ICommandTaskResult& result) { CompleteCommand(writer, requestId, result); };
    if (mode == "SelectBeforeOperate") master->SelectAndOperate(command, index, callback);
    else if (mode == "DirectOperate") master->DirectOperate(command, index, callback);
    else throw std::invalid_argument("Unsupported DNP3 command mode: " + mode);
}

void ExecuteAnalog(const std::vector<std::string>& fields, const std::shared_ptr<IMaster>& master, ProtocolWriter& writer)
{
    if (fields.size() != 7) throw std::invalid_argument("ANALOG command requires 7 fields.");
    const auto requestId = ParseInteger<uint64_t>(fields[2], "request id");
    const auto index = ParseInteger<uint16_t>(fields[3], "command index");
    const auto variation = fields[4];
    const auto mode = fields[5];
    const auto& value = fields[6];
    auto callback = [&writer, requestId](const ICommandTaskResult& result) { CompleteCommand(writer, requestId, result); };

    auto execute = [&](const auto& command) {
        if (mode == "SelectBeforeOperate") master->SelectAndOperate(command, index, callback);
        else if (mode == "DirectOperate") master->DirectOperate(command, index, callback);
        else throw std::invalid_argument("Unsupported DNP3 command mode: " + mode);
    };

    if (variation == "Int32") execute(AnalogOutputInt32(ParseInteger<int32_t>(value, "G41V1 value")));
    else if (variation == "Int16") execute(AnalogOutputInt16(ParseInteger<int16_t>(value, "G41V2 value")));
    else if (variation == "Float32") execute(AnalogOutputFloat32(std::stof(value)));
    else if (variation == "Float64") execute(AnalogOutputDouble64(std::stod(value)));
    else throw std::invalid_argument("Unsupported DNP3 analog output variation: " + variation);
}

TimeSyncMode ParseTimeSyncMode(const std::string& value)
{
    if (value == "Disabled") return TimeSyncMode::None;
    if (value == "Lan") return TimeSyncMode::LAN;
    if (value == "NonLan") return TimeSyncMode::NonLAN;
    throw std::invalid_argument("Unsupported DNP3 time synchronization mode: " + value);
}

std::vector<std::string> ClassDiagnostics(uint8_t classes)
{
    std::vector<std::string> diagnostics;
    if ((classes & ClassField::CLASS_0) != 0) diagnostics.emplace_back("CLASS0_SCAN");
    if ((classes & ClassField::CLASS_1) != 0) diagnostics.emplace_back("CLASS1_SCAN");
    if ((classes & ClassField::CLASS_2) != 0) diagnostics.emplace_back("CLASS2_SCAN");
    if ((classes & ClassField::CLASS_3) != 0) diagnostics.emplace_back("CLASS3_SCAN");
    return diagnostics;
}

void AddOptionalScan(std::vector<std::shared_ptr<IMasterScan>>& scans,
                     const std::shared_ptr<IMaster>& master,
                     const std::shared_ptr<ISOEHandler>& handler,
                     ProtocolWriter& writer,
                     uint8_t classes,
                     int64_t intervalMs,
                     std::vector<std::string> diagnostics)
{
    if (intervalMs <= 0) return;
    auto callback = std::make_shared<ScanDiagnosticCallback>(writer, std::move(diagnostics));
    scans.push_back(master->AddClassScan(
        ClassField(classes),
        TimeDuration::Milliseconds(intervalMs),
        handler,
        TaskConfig::With(callback)));
}

int Run(int argc, char** argv)
{
    const auto config = ParseConfig(argc, argv);
    ProtocolWriter writer;
    std::atomic<bool> stopping{false};

    DNP3Manager manager(1);
    auto listener = std::make_shared<EliteChannelListener>(writer, stopping);
    const ChannelRetry retry(TimeDuration::Milliseconds(config.reconnectMinMs), TimeDuration::Milliseconds(config.reconnectMaxMs));
    auto channel = manager.AddTCPClient("elitescada-dnp3", levels::NORMAL, retry,
                                        {IPEndpoint(config.host, config.port)}, "0.0.0.0", listener);

    MasterStackConfig stack;
    stack.master.responseTimeout = TimeDuration::Milliseconds(config.responseTimeoutMs);
    stack.master.taskRetryPeriod = TimeDuration::Milliseconds(config.reconnectMinMs);
    stack.master.maxTaskRetryPeriod = TimeDuration::Milliseconds(config.reconnectMaxMs);
    stack.master.taskStartTimeout = TimeDuration::Milliseconds(config.responseTimeoutMs);
    stack.master.startupIntegrityClassMask = ClassField(config.startupClasses);
    stack.master.disableUnsolOnStartup = config.disableUnsolicitedClasses != 0;
    stack.master.unsolClassMask = ClassField(config.enableUnsolicitedClasses);
    stack.master.eventScanOnEventsAvailableClassMask = ClassField(config.eventScanClasses);
    stack.master.integrityOnEventOverflowIIN = config.integrityOnOverflow;
    stack.master.timeSyncMode = ParseTimeSyncMode(config.timeSync);
    stack.link.LocalAddr = config.masterAddress;
    stack.link.RemoteAddr = config.outstationAddress;
    stack.link.Timeout = TimeDuration::Milliseconds(config.responseTimeoutMs);
    stack.link.KeepAliveTimeout = config.keepAliveMs < 0 ? TimeDuration::Max() : TimeDuration::Milliseconds(config.keepAliveMs);

    auto soe = std::make_shared<EliteSOEHandler>(writer);
    auto app = std::make_shared<EliteMasterApplication>(writer, config.startupClasses);
    auto master = channel->AddMaster("elitescada-master", soe, app, stack);
    std::vector<std::shared_ptr<IMasterScan>> scans;
    AddOptionalScan(scans, master, soe, writer, config.startupClasses, config.integrityPollMs, ClassDiagnostics(config.startupClasses));
    AddOptionalScan(scans, master, soe, writer, ClassField::CLASS_1, config.class1PollMs, {"CLASS1_SCAN"});
    AddOptionalScan(scans, master, soe, writer, ClassField::CLASS_2, config.class2PollMs, {"CLASS2_SCAN"});
    AddOptionalScan(scans, master, soe, writer, ClassField::CLASS_3, config.class3PollMs, {"CLASS3_SCAN"});

    master->Enable();
    writer.Ready();

    std::string line;
    while (std::getline(std::cin, line))
    {
        if (line.empty()) continue;
        try
        {
            const auto fields = SplitTabs(line);
            if (fields.size() < 2 || fields[0] != ProtocolVersion) throw std::invalid_argument("Unsupported host command protocol.");
            if (fields[1] == "STOP")
            {
                stopping.store(true);
                writer.State("Stopping");
                manager.Shutdown();
                writer.State("Stopped");
                return 0;
            }
            if (fields[1] == "BINARY") ExecuteBinary(fields, master, writer);
            else if (fields[1] == "ANALOG") ExecuteAnalog(fields, master, writer);
            else throw std::invalid_argument("Unsupported host command: " + fields[1]);
        }
        catch (const std::exception& ex)
        {
            std::cerr << "OpenDNP3 host command error: " << ex.what() << std::endl;
        }
    }

    stopping.store(true);
    manager.Shutdown();
    return 0;
}
}

int main(int argc, char** argv)
{
    try
    {
        return Run(argc, argv);
    }
    catch (const std::exception& ex)
    {
        std::cerr << "OpenDNP3 host fatal error: " << ex.what() << std::endl;
        return 2;
    }
}
