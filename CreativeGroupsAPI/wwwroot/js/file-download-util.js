/**
 * File Download Utility for Creative Groups API
 * Handles binary file downloads from the backend API
 */

class FileDownloadUtil {
    constructor(baseUrl = '/api/PayrollUpload') {
        this.baseUrl = baseUrl;
    }

    /**
     * Download PF Report (ECR Challan)
     * @param {number} companyId - Company ID
     * @param {number} payrollMonthId - Payroll Month ID
     * @returns {Promise<void>}
     */
    async downloadPFReport(companyId, payrollMonthId) {
        if (!companyId || !payrollMonthId) {
            throw new Error('Company ID and Payroll Month ID are required');
        }

        try {
            console.log(`Starting PF download for company ${companyId}, payroll month ${payrollMonthId}`);
            
            const response = await fetch(`${this.baseUrl}/download-pf/${companyId}/${payrollMonthId}`, {
                method: 'GET',
                headers: {
                    'Accept': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
                    'Cache-Control': 'no-cache'
                }
            });

            if (!response.ok) {
                // Handle error responses (these will be JSON)
                let errorMessage = 'Download failed';
                try {
                    const errorData = await response.json();
                    errorMessage = errorData.message || errorMessage;
                } catch (jsonError) {
                    // If error response is not JSON, use status text
                    errorMessage = response.statusText || errorMessage;
                }
                throw new Error(errorMessage);
            }

            // Handle successful file download as binary
            const blob = await response.blob();
            
            // Verify we got a valid Excel file
            if (blob.type && !blob.type.includes('spreadsheetml') && !blob.type.includes('excel')) {
                console.warn('Warning: Response does not appear to be an Excel file:', blob.type);
            }
            
            // Get filename from Content-Disposition header if available
            let filename = `ECR_Challan_${new Date().toISOString().slice(0,10)}.xlsx`;
            const contentDisposition = response.headers.get('Content-Disposition');
            if (contentDisposition) {
                const matches = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
                if (matches && matches[1]) {
                    filename = matches[1].replace(/['"]/g, '');
                }
            }
            
            this._downloadBlob(blob, filename);
            
            console.log(`Successfully downloaded: ${filename}`);
            return { success: true, filename };
            
        } catch (error) {
            console.error('PF Download error:', error);
            throw error;
        }
    }

    /**
     * Check if reports can be downloaded
     * @param {number} companyId - Company ID
     * @param {number} payrollMonthId - Payroll Month ID
     * @returns {Promise<Object>}
     */
    async canDownloadReports(companyId, payrollMonthId) {
        if (!companyId || !payrollMonthId) {
            throw new Error('Company ID and Payroll Month ID are required');
        }

        try {
            const response = await fetch(`${this.baseUrl}/can-download/${companyId}/${payrollMonthId}`);
            const data = await response.json();
            
            if (response.ok) {
                return { canDownload: data.canDownload, message: data.message };
            } else {
                throw new Error(data.message || 'Cannot check download eligibility');
            }
        } catch (error) {
            console.error('Can download check error:', error);
            throw error;
        }
    }

    /**
     * Alternative download method using form submission
     * Useful for browsers that have issues with fetch for file downloads
     * @param {number} companyId - Company ID
     * @param {number} payrollMonthId - Payroll Month ID
     */
    downloadPFReportViaForm(companyId, payrollMonthId) {
        if (!companyId || !payrollMonthId) {
            throw new Error('Company ID and Payroll Month ID are required');
        }

        const form = document.createElement('form');
        form.method = 'GET';
        form.action = `${this.baseUrl}/download-pf/${companyId}/${payrollMonthId}`;
        form.style.display = 'none';
        form.target = '_blank'; // Open in new tab/window
        
        document.body.appendChild(form);
        form.submit();
        document.body.removeChild(form);
        
        console.log('PF download initiated via form submission');
    }

    /**
     * Private method to download blob as file
     * @param {Blob} blob - The file blob
     * @param {string} filename - The filename
     * @private
     */
    _downloadBlob(blob, filename) {
        const url = window.URL.createObjectURL(blob);
        
        try {
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            a.style.display = 'none';
            
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
        } finally {
            // Always cleanup the URL
            window.URL.revokeObjectURL(url);
        }
    }
}

// Usage examples:
/*
// Initialize the utility
const downloader = new FileDownloadUtil();

// Download PF Report
try {
    await downloader.downloadPFReport(1, 1);
    console.log('Download completed successfully');
} catch (error) {
    console.error('Download failed:', error.message);
}

// Check if download is allowed
try {
    const result = await downloader.canDownloadReports(1, 1);
    if (result.canDownload) {
        console.log('Download is allowed');
    } else {
        console.log('Download not allowed:', result.message);
    }
} catch (error) {
    console.error('Check failed:', error.message);
}

// Alternative form-based download
downloader.downloadPFReportViaForm(1, 1);
*/

// Export for use in modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = FileDownloadUtil;
}

// Make available globally
if (typeof window !== 'undefined') {
    window.FileDownloadUtil = FileDownloadUtil;
}
