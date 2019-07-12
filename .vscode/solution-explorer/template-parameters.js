var path = require("path");

module.exports = function(filename, projectPath, folderPath) {
    var namespace = "Unknown";
    if (projectPath) {
        namespace = path.basename(projectPath, path.extname(projectPath));
        if (folderPath) {
            namespace += "." + folderPath.replace(path.dirname(projectPath), "").substring(1).replace(/[\\\/]/g, ".");
        }
        namespace = namespace.replace(/[\\\-]/g, "_");
    }       

    return {
        namespace: namespace,
        name: path.basename(filename, path.extname(filename))
    }
};