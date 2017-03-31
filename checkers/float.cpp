#include <iostream>
#include <vector>
#include <cstdlib>

using namespace std;

int usrCount, corrCount, paramCount;
vector<double> usr;
vector<double> corr;
double eps;

bool same(double u, double c) {
    return abs(u-c) <= eps;
}

int main() {
    cin >> usrCount >> corrCount >> paramCount;
    for (int i = 0; i < usrCount; ++i) {
        double cur;
        cin >> cur;
        usr.push_back(cur);
    }
    for (int i = 0; i < corrCount; ++i) {
        double cur;
        cin >> cur;
        corr.push_back(cur);
    }
    if (paramCount != 1) return 1; // eps missing
    cin >> eps;
    
    if (usrCount != corrCount) {
        cout << "WA\n";
        return 0;
    }
    
    for (int i = 0; i < usrCount; ++i) {
        if (!same(usr[i], corr[i])) {
            cout << "WA\n";
            return 0;
        }
    }
    cout << "OK\n";
    return 0;
}